import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type Dispatch,
  type SetStateAction,
} from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '@/features/auth/components/AuthProvider'
import { lookupApi, type LookupItem } from '@/features/lookups/api/lookupApi'
import { listingApi, type ListingItem } from '@/features/listings/api/listingApi'
import { ListingHomeFilters, type ExpandableFilterKey, type QuickFilterKey } from '@/features/listings/components/ListingHomeFilters'
import { ListingHomeGrid } from '@/features/listings/components/ListingHomeGrid'
import { ListingHomeHero } from '@/features/listings/components/ListingHomeHero'
import { ListingHomeMarquee } from '@/features/listings/components/ListingHomeMarquee'
import { PurchaseConfirmModal } from '@/features/listings/components/PurchaseConfirmModal'
import { TopPinIntroModal } from '@/features/listings/components/TopPinIntroModal'
import {
  TOP_PIN_FOCUS_QUERY,
  TOP_PIN_SECTION_HASH,
  TOP_PIN_SKIP_INTRO_STORAGE_KEY,
} from '@/features/listings/constants/topPin'
import { messagingApi } from '@/features/messaging/api/messagingApi'
import { useSharedMessageHub } from '@/features/messaging/context/SharedMessageHubProvider'
import { ApiClientError } from '@/shared/types/api'

const PAGE_SIZE = 12
const FILTER_SCROLL_NAV_OFFSET_PX = 104
const FILTER_SCROLL_DURATION_MS = 700
const FILTER_SCROLL_BROWSE_DURATION_MS = 1500

const easeInOutCubic = (progress: number) =>
  progress < 0.5 ? 4 * progress * progress * progress : 1 - Math.pow(-2 * progress + 2, 3) / 2

const isMobileViewport = () => window.matchMedia('(max-width: 767px)').matches

const LINE_NOTIFY_ICON = new URL('../../../png/line_icon.png', import.meta.url).href
const EMAIL_NOTIFY_ICON = new URL('../../../png/email_icon.png', import.meta.url).href
const QUICK_RESPONDER_ICON = new URL('../../../png/fastrespone_icon.png', import.meta.url).href

export const ListingHomePage = () => {
  const navigate = useNavigate()
  const { isAuthenticated, tokens } = useAuth()
  const { totalUnread: unreadMessageCount } = useSharedMessageHub()
  const queryClient = useQueryClient()
  const [page, setPage] = useState(1)
  const [selectedCategoryCodes, setSelectedCategoryCodes] = useState<number[]>([])
  const [selectedConditionCodes, setSelectedConditionCodes] = useState<number[]>([])
  const [selectedResidenceCodes, setSelectedResidenceCodes] = useState<number[]>([])
  const [isFree, setIsFree] = useState(false)
  const [isCharity, setIsCharity] = useState(false)
  const [isTradeable, setIsTradeable] = useState(false)
  const [expandedFilter, setExpandedFilter] = useState<ExpandableFilterKey | null>(null)
  const [mobileSheetFilter, setMobileSheetFilter] = useState<ExpandableFilterKey | null>(null)
  const [quickFilterHover, setQuickFilterHover] = useState<QuickFilterKey | null>(null)
  const [quickFilterPlayNonce, setQuickFilterPlayNonce] = useState(0)
  const [filtersInView, setFiltersInView] = useState(false)
  const [favoriteStateById, setFavoriteStateById] = useState<
    Record<ListingItem['id'], { isFavorited: boolean; favoriteCount: number }>
  >({})
  const [favoriteBusyIds, setFavoriteBusyIds] = useState<Set<ListingItem['id']>>(() => new Set())
  const [conversationBusyIds, setConversationBusyIds] = useState<Set<ListingItem['id']>>(() => new Set())
  const [purchaseBusyIds, setPurchaseBusyIds] = useState<Set<ListingItem['id']>>(() => new Set())
  const [purchaseConfirmTarget, setPurchaseConfirmTarget] = useState<ListingItem | null>(null)
  const [countdownNowMs, setCountdownNowMs] = useState(() => Date.now())
  const [topPinTargetId, setTopPinTargetId] = useState<string | null>(null)
  const [topPinSkipIntro, setTopPinSkipIntro] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [loadedItems, setLoadedItems] = useState<ListingItem[]>([])
  const [knownTotalPages, setKnownTotalPages] = useState(1)
  const listingSectionRef = useRef<HTMLElement | null>(null)
  const filterSectionRef = useRef<HTMLElement | null>(null)
  const desktopFilterRowRef = useRef<HTMLDivElement | null>(null)
  const mobileFilterSheetWasOpenRef = useRef(false)
  const skipMobileFilterScrollRef = useRef(true)
  const filterScrollFrameRef = useRef<number | null>(null)
  const desktopFilterAreaRef = useRef<HTMLDivElement | null>(null)
  const quickFilterHoverTimerRef = useRef<number | null>(null)
  const loadMoreSentinelRef = useRef<HTMLDivElement | null>(null)

  const listingFiltersPayload = useMemo(
    () => ({
      page,
      categoryCodes: [...selectedCategoryCodes].sort((a, b) => a - b),
      conditionCodes: [...selectedConditionCodes].sort((a, b) => a - b),
      residenceCodes: [...selectedResidenceCodes].sort((a, b) => a - b),
      isFree,
      isCharity,
      isTradeable,
    }),
    [page, selectedCategoryCodes, selectedConditionCodes, selectedResidenceCodes, isFree, isCharity, isTradeable],
  )
  const listingFilterSignature = useMemo(
    () =>
      JSON.stringify({
        categoryCodes: listingFiltersPayload.categoryCodes,
        conditionCodes: listingFiltersPayload.conditionCodes,
        residenceCodes: listingFiltersPayload.residenceCodes,
        isFree: listingFiltersPayload.isFree,
        isCharity: listingFiltersPayload.isCharity,
        isTradeable: listingFiltersPayload.isTradeable,
      }),
    [
      listingFiltersPayload.categoryCodes,
      listingFiltersPayload.conditionCodes,
      listingFiltersPayload.residenceCodes,
      listingFiltersPayload.isFree,
      listingFiltersPayload.isCharity,
      listingFiltersPayload.isTradeable,
    ],
  )

  const lookupsQuery = useQuery({
    queryKey: ['lookups', 'listingFilters'],
    queryFn: async () => {
      const [categoryResult, conditionResult, residenceResult] = await Promise.all([
        lookupApi.categories(),
        lookupApi.conditions(),
        lookupApi.residences(),
      ])
      return { categories: categoryResult, conditions: conditionResult, residences: residenceResult }
    },
    staleTime: 5 * 60 * 1000,
  })

  const listQuery = useQuery({
    queryKey: ['listings', 'home', listingFiltersPayload],
    queryFn: () =>
      listingApi.list({
        page: listingFiltersPayload.page,
        pageSize: PAGE_SIZE,
        categoryCodes: listingFiltersPayload.categoryCodes,
        conditionCodes: listingFiltersPayload.conditionCodes,
        residenceCodes: listingFiltersPayload.residenceCodes,
        isFree: listingFiltersPayload.isFree || undefined,
        isCharity: listingFiltersPayload.isCharity || undefined,
        isTradeable: listingFiltersPayload.isTradeable || undefined,
      }),
    placeholderData: (previousData) => previousData,
    staleTime: 60_000,
  })

  const categories: LookupItem[] = lookupsQuery.data?.categories ?? []
  const conditions: LookupItem[] = lookupsQuery.data?.conditions ?? []
  const residences: LookupItem[] = lookupsQuery.data?.residences ?? []
  const items = loadedItems
  const totalPages = knownTotalPages
  const hasMorePages = page < totalPages
  const loading = listQuery.isFetching
  const showLoadingSkeleton = listQuery.isPending && page === 1 && !loadedItems.length

  const prefetchListingDetail = useCallback(
    (id: ListingItem['id']) => {
      void queryClient.prefetchQuery({
        queryKey: ['listings', 'detail', id],
        queryFn: () => listingApi.getById(id),
        staleTime: 60_000,
      })
    },
    [queryClient],
  )

  useEffect(() => {
    setTopPinSkipIntro(window.localStorage.getItem(TOP_PIN_SKIP_INTRO_STORAGE_KEY) === '1')
  }, [])

  useEffect(() => {
    if (lookupsQuery.isError) {
      setError('載入篩選條件失敗，請稍後再試')
    }
  }, [lookupsQuery.isError])

  useEffect(() => {
    if (listQuery.isFetching) {
      setError(null)
    }
  }, [listQuery.isFetching])

  useEffect(() => {
    if (!listQuery.isError) {
      return
    }
    const message = listQuery.error instanceof ApiClientError ? listQuery.error.message : '讀取商品列表失敗'
    setError(message)
  }, [listQuery.isError, listQuery.error])

  useEffect(() => {
    setPage(1)
    setKnownTotalPages(1)
  }, [listingFilterSignature])

  const scrollToFilterSection = useCallback((durationMs = FILTER_SCROLL_DURATION_MS) => {
    const target = filterSectionRef.current ?? listingSectionRef.current
    const targetTop = target?.getBoundingClientRect().top
    if (targetTop == null) {
      return
    }

    if (filterScrollFrameRef.current !== null) {
      window.cancelAnimationFrame(filterScrollFrameRef.current)
      filterScrollFrameRef.current = null
    }

    const startY = window.scrollY
    const destinationY = Math.max(0, startY + targetTop - FILTER_SCROLL_NAV_OFFSET_PX)
    const distance = destinationY - startY
    if (Math.abs(distance) < 2) {
      return
    }

    const startedAt = performance.now()

    const animateScroll = (now: number) => {
      const elapsed = now - startedAt
      const progress = Math.min(elapsed / durationMs, 1)
      const easedProgress = easeInOutCubic(progress)

      window.scrollTo({
        top: startY + distance * easedProgress,
        behavior: 'auto',
      })

      if (progress < 1) {
        filterScrollFrameRef.current = window.requestAnimationFrame(animateScroll)
      } else {
        filterScrollFrameRef.current = null
      }
    }

    filterScrollFrameRef.current = window.requestAnimationFrame(animateScroll)
  }, [])

  useEffect(() => {
    return () => {
      if (filterScrollFrameRef.current !== null) {
        window.cancelAnimationFrame(filterScrollFrameRef.current)
      }
    }
  }, [])

  useEffect(() => {
    const sheetOpen = mobileSheetFilter !== null
    const wasOpen = mobileFilterSheetWasOpenRef.current
    mobileFilterSheetWasOpenRef.current = sheetOpen

    if (!wasOpen || sheetOpen || !isMobileViewport()) {
      return
    }

    window.requestAnimationFrame(() => {
      scrollToFilterSection()
    })
  }, [mobileSheetFilter, scrollToFilterSection])

  useEffect(() => {
    if (skipMobileFilterScrollRef.current) {
      skipMobileFilterScrollRef.current = false
      return
    }

    if (mobileSheetFilter || !isMobileViewport()) {
      return
    }

    window.requestAnimationFrame(() => {
      scrollToFilterSection()
    })
  }, [listingFilterSignature, mobileSheetFilter, scrollToFilterSection])

  useEffect(() => {
    const response = listQuery.data
    if (!response) {
      return
    }

    setKnownTotalPages(response.pagination.totalPages || 1)
    setLoadedItems((current) => {
      if (page <= 1) {
        return response.items
      }

      const merged = [...current]
      response.items.forEach((item) => {
        if (!merged.some((existing) => existing.id === item.id)) {
          merged.push(item)
        }
      })
      return merged
    })
  }, [listQuery.data, page])

  useEffect(() => {
    setFavoriteStateById(() => {
      const next: Record<ListingItem['id'], { isFavorited: boolean; favoriteCount: number }> = {}
      items.forEach((item) => {
        next[item.id] = {
          isFavorited: item.isFavorited,
          favoriteCount: item.favoriteCount,
        }
      })
      return next
    })
  }, [items])

  useEffect(() => {
    const hasPendingCountdown = items.some(
      (item) => Boolean(item.pendingPurchaseRequestExpireAt) || (item.pendingPurchaseRequestRemainingSeconds ?? 0) > 0,
    )
    if (!hasPendingCountdown) {
      return
    }

    const timer = window.setInterval(() => {
      setCountdownNowMs(Date.now())
    }, 1000)

    return () => {
      window.clearInterval(timer)
    }
  }, [items])

  useEffect(() => {
    const target = desktopFilterRowRef.current
    if (!target) {
      return
    }

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          setFiltersInView(true)
          observer.disconnect()
        }
      },
      { threshold: 0.2 },
    )

    observer.observe(target)
    return () => observer.disconnect()
  }, [])

  useEffect(() => {
    if (!expandedFilter) {
      return
    }

    const closeOnOutsideClick = (event: PointerEvent) => {
      const container = desktopFilterAreaRef.current
      const target = event.target as Node | null
      if (!container || !target) {
        return
      }

      if (!container.contains(target)) {
        setExpandedFilter(null)
      }
    }

    document.addEventListener('pointerdown', closeOnOutsideClick, true)
    return () => {
      document.removeEventListener('pointerdown', closeOnOutsideClick, true)
    }
  }, [expandedFilter])

  useEffect(() => {
    const target = loadMoreSentinelRef.current
    if (!target) {
      return
    }

    const observer = new IntersectionObserver(
      (entries) => {
        if (!entries.some((entry) => entry.isIntersecting)) {
          return
        }
        if (listQuery.isFetching || listQuery.isPending || page >= totalPages) {
          return
        }
        setPage((current) => current + 1)
      },
      { rootMargin: '280px 0px', threshold: 0.1 },
    )

    observer.observe(target)
    return () => observer.disconnect()
  }, [listQuery.isFetching, listQuery.isPending, page, totalPages])

  const toggleMultiCode = (
    value: number,
    setValues: Dispatch<SetStateAction<number[]>>,
  ) => {
    setPage(1)
    setValues((current) =>
      current.includes(value) ? current.filter((x) => x !== value) : [...current, value],
    )
  }

  const clearAllFilters = () => {
    setPage(1)
    setSelectedCategoryCodes([])
    setSelectedConditionCodes([])
    setSelectedResidenceCodes([])
    setIsFree(false)
    setIsCharity(false)
    setIsTradeable(false)
    setExpandedFilter(null)
    setMobileSheetFilter(null)
  }

  const handleBrowseListings = () => {
    scrollToFilterSection(FILTER_SCROLL_BROWSE_DURATION_MS)
  }

  const closeMobileFilterSheet = () => {
    setMobileSheetFilter(null)
  }

  const triggerQuickFilterLottie = (key: QuickFilterKey) => {
    if (quickFilterHoverTimerRef.current !== null) {
      window.clearTimeout(quickFilterHoverTimerRef.current)
    }

    quickFilterHoverTimerRef.current = window.setTimeout(() => {
      setQuickFilterHover(key)
      setQuickFilterPlayNonce((current) => current + 1)
      quickFilterHoverTimerRef.current = null
    }, 200)
  }

  const clearQuickFilterLottie = () => {
    if (quickFilterHoverTimerRef.current !== null) {
      window.clearTimeout(quickFilterHoverTimerRef.current)
      quickFilterHoverTimerRef.current = null
    }
    setQuickFilterHover(null)
  }

  const updateBusySet = (
    itemId: ListingItem['id'],
    busy: boolean,
    setState: Dispatch<SetStateAction<Set<ListingItem['id']>>>,
  ) => {
    setState((current) => {
      const next = new Set(current)
      if (busy) {
        next.add(itemId)
      } else {
        next.delete(itemId)
      }
      return next
    })
  }

  const toggleFavorite = useCallback(async (item: ListingItem) => {
    if (!isAuthenticated) {
      navigate('/login')
      return
    }

    if (favoriteBusyIds.has(item.id)) {
      return
    }

    updateBusySet(item.id, true, setFavoriteBusyIds)
    setError(null)

    try {
      const currentState = favoriteStateById[item.id]
      const payload = currentState?.isFavorited
        ? await listingApi.unfavorite(item.id)
        : await listingApi.favorite(item.id)

      setFavoriteStateById((current) => ({
        ...current,
        [item.id]: {
          isFavorited: payload.isFavorited,
          favoriteCount: payload.favoriteCount,
        },
      }))
    } catch (err) {
      const message = err instanceof ApiClientError ? err.message : '收藏操作失敗'
      setError(message)
    } finally {
      updateBusySet(item.id, false, setFavoriteBusyIds)
    }
  }, [favoriteBusyIds, favoriteStateById, isAuthenticated, navigate])

  const startConversation = useCallback(async (item: ListingItem) => {
    if (!isAuthenticated) {
      navigate('/login')
      return
    }

    if (tokens?.userId === item.seller.id) {
      setError('這是你的商品，無法與自己建立對話')
      return
    }

    if (conversationBusyIds.has(item.id)) {
      return
    }

    updateBusySet(item.id, true, setConversationBusyIds)
    setError(null)

    try {
      const conversationId = await messagingApi.ensureConversation(item.id, item.seller.id)
      navigate(`/messages/${conversationId}`)
    } catch (err) {
      const message = err instanceof ApiClientError ? err.message : '建立對話失敗'
      setError(message)
    } finally {
      updateBusySet(item.id, false, setConversationBusyIds)
    }
  }, [conversationBusyIds, isAuthenticated, navigate, tokens?.userId])

  const handlePurchase = async (item: ListingItem) => {
    if (!isAuthenticated) {
      navigate('/login')
      return
    }

    if (tokens?.userId === item.seller.id) {
      setError('這是你的商品，無法購買自己的商品')
      return
    }

    if (purchaseBusyIds.has(item.id)) {
      return
    }

    updateBusySet(item.id, true, setPurchaseBusyIds)
    setError(null)

    try {
      const request = await listingApi.createPurchaseRequest(item.id)
      navigate(`/messages/${request.conversationId}`)
    } catch (err) {
      const message = err instanceof ApiClientError ? err.message : '送出購買請求失敗'
      setError(message)
    } finally {
      updateBusySet(item.id, false, setPurchaseBusyIds)
    }
  }

  const openPurchaseConfirm = useCallback((item: ListingItem) => {
    if (!isAuthenticated) {
      navigate('/login')
      return
    }

    if (tokens?.userId === item.seller.id) {
      setError('這是你的商品，無法購買自己的商品')
      return
    }

    setPurchaseConfirmTarget(item)
  }, [isAuthenticated, navigate, tokens?.userId])

  const confirmPurchase = () => {
    if (!purchaseConfirmTarget) {
      return
    }

    const target = purchaseConfirmTarget
    setPurchaseConfirmTarget(null)
    void handlePurchase(target)
  }

  const navigateToTopPinSection = (itemId: string) => {
    navigate(`/listings/${itemId}/edit?focus=${TOP_PIN_FOCUS_QUERY}${TOP_PIN_SECTION_HASH}`)
  }

  const openTopPinFlow = useCallback((item: ListingItem) => {
    if (!isAuthenticated) {
      navigate('/login')
      return
    }

    if (!topPinSkipIntro) {
      setTopPinTargetId(item.id)
      return
    }

    navigateToTopPinSection(item.id)
  }, [isAuthenticated, navigate, topPinSkipIntro])

  const handleTopPinConfirm = (skipNextReminder: boolean) => {
    if (!topPinTargetId) {
      return
    }

    if (skipNextReminder) {
      window.localStorage.setItem(TOP_PIN_SKIP_INTRO_STORAGE_KEY, '1')
      setTopPinSkipIntro(true)
    }

    const listingId = topPinTargetId
    setTopPinTargetId(null)
    navigateToTopPinSection(listingId)
  }

  const handleTopPinSubmission = () => {
    setTopPinTargetId(null)
    navigate('/top-pin-submissions/create')
  }

  return (
    <main className="mx-auto flex w-full max-w-6xl flex-col px-4 py-6 md:py-8">
      <ListingHomeHero unreadMessageCount={unreadMessageCount} onBrowseListings={handleBrowseListings} />
      <ListingHomeMarquee />
      <div className="order-3 md:order-3">
        <ListingHomeFilters
          listingSectionRef={listingSectionRef}
          filterSectionRef={filterSectionRef}
          desktopFilterAreaRef={desktopFilterAreaRef}
          desktopFilterRowRef={desktopFilterRowRef}
          filtersInView={filtersInView}
          categories={categories}
          conditions={conditions}
          residences={residences}
          selectedCategoryCodes={selectedCategoryCodes}
          selectedConditionCodes={selectedConditionCodes}
          selectedResidenceCodes={selectedResidenceCodes}
          setSelectedCategoryCodes={setSelectedCategoryCodes}
          setSelectedConditionCodes={setSelectedConditionCodes}
          setSelectedResidenceCodes={setSelectedResidenceCodes}
          isFree={isFree}
          isCharity={isCharity}
          isTradeable={isTradeable}
          setIsFree={setIsFree}
          setIsCharity={setIsCharity}
          setIsTradeable={setIsTradeable}
          expandedFilter={expandedFilter}
          setExpandedFilter={setExpandedFilter}
          mobileSheetFilter={mobileSheetFilter}
          setMobileSheetFilter={setMobileSheetFilter}
          quickFilterHover={quickFilterHover}
          quickFilterPlayNonce={quickFilterPlayNonce}
          setPage={setPage}
          onToggleMultiCode={toggleMultiCode}
          onClearAllFilters={clearAllFilters}
          onCloseMobileFilterSheet={closeMobileFilterSheet}
          onTriggerQuickFilterLottie={triggerQuickFilterLottie}
          onClearQuickFilterLottie={clearQuickFilterLottie}
        />
        {error ? <p className="mb-4 text-sm text-danger">{error}</p> : null}
        <ListingHomeGrid
          items={items}
          loading={loading}
          showLoadingSkeleton={showLoadingSkeleton}
          page={page}
          totalPages={totalPages}
          hasMorePages={hasMorePages}
          countdownNowMs={countdownNowMs}
          currentUserId={tokens?.userId}
          favoriteStateById={favoriteStateById}
          favoriteBusyIds={favoriteBusyIds}
          conversationBusyIds={conversationBusyIds}
          purchaseBusyIds={purchaseBusyIds}
          lineNotifyIcon={LINE_NOTIFY_ICON}
          emailNotifyIcon={EMAIL_NOTIFY_ICON}
          quickResponderIcon={QUICK_RESPONDER_ICON}
          loadMoreSentinelRef={loadMoreSentinelRef}
          onPrefetchListingDetail={prefetchListingDetail}
          onToggleFavorite={toggleFavorite}
          onOpenTopPinFlow={openTopPinFlow}
          onStartConversation={startConversation}
          onOpenPurchaseConfirm={openPurchaseConfirm}
        />
      </div>
      <TopPinIntroModal
        open={topPinTargetId !== null}
        onClose={() => setTopPinTargetId(null)}
        onConfirmTopPin={handleTopPinConfirm}
        onGoSubmission={handleTopPinSubmission}
      />
      <PurchaseConfirmModal
        open={purchaseConfirmTarget !== null}
        listingTitle={purchaseConfirmTarget?.title ?? ''}
        busy={purchaseConfirmTarget ? purchaseBusyIds.has(purchaseConfirmTarget.id) : false}
        onClose={() => setPurchaseConfirmTarget(null)}
        onConfirm={confirmPurchase}
      />
    </main>
  )
}
