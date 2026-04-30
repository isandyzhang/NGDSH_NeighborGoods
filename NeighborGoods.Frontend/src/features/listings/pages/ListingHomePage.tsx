import { useEffect, useRef, useState, type Dispatch, type ReactNode, type SetStateAction } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { DotLottieReact } from '@lottiefiles/dotlottie-react'
import { AnimatePresence, motion } from 'framer-motion'
import {
  Baby,
  BookText,
  Dumbbell,
  Gamepad2,
  Home,
  MonitorSmartphone,
  Package,
  Rocket,
  Shirt,
  Sofa,
  UtensilsCrossed,
} from 'lucide-react'
import { useAuth } from '@/features/auth/components/AuthProvider'
import { lookupApi, type LookupItem } from '@/features/lookups/api/lookupApi'
import { listingApi, type ListingItem } from '@/features/listings/api/listingApi'
import { PurchaseConfirmModal } from '@/features/listings/components/PurchaseConfirmModal'
import { TopPinIntroModal } from '@/features/listings/components/TopPinIntroModal'
import {
  TOP_PIN_FOCUS_QUERY,
  TOP_PIN_SECTION_HASH,
  TOP_PIN_SKIP_INTRO_STORAGE_KEY,
} from '@/features/listings/constants/topPin'
import { messagingApi } from '@/features/messaging/api/messagingApi'
import { ApiClientError } from '@/shared/types/api'
import { Button, getButtonClassName } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'

const PAGE_SIZE = 12
const MIN_SKELETON_MS = 180

type ExpandableFilterKey = 'category' | 'condition' | 'residence'
type QuickFilterKey = 'free' | 'charity' | 'tradeable'

const LOCAL_GIFT_LOTTIE = new URL('../../../lottie/gift icons.lottie', import.meta.url).href
const LOCAL_FAVORITES_LOTTIE = new URL('../../../lottie/Add to favorites.lottie', import.meta.url).href
const LOCAL_EXCHANGE_LOTTIE = new URL('../../../lottie/Exchange.lottie', import.meta.url).href
const LINE_NOTIFY_ICON = new URL('../../../png/line_icon.png', import.meta.url).href
const EMAIL_NOTIFY_ICON = new URL('../../../png/email_icon.png', import.meta.url).href
const QUICK_RESPONDER_ICON = new URL('../../../png/fastrespone_icon.png', import.meta.url).href

const QUICK_FILTER_LOTTIE: Record<QuickFilterKey, string> = {
  free: LOCAL_GIFT_LOTTIE,
  charity: LOCAL_FAVORITES_LOTTIE,
  tradeable: LOCAL_EXCHANGE_LOTTIE,
}

const MARQUEE_NOTICE_TEXT =
  '交易安全提醒：請勿使用匯款或寄送包裹，本網站僅提供面交交易服務。請事先約定地點並準時赴約，面交時請雙方當場確認商品狀況與數量；若為高價物品，建議先交換其他聯絡方式。本站不介入且不負責處理交易糾紛。'

const formatPrice = (item: ListingItem) => {
  if (item.isFree) {
    return '免費'
  }

  return `NT$ ${item.price.toLocaleString()}`
}

const formatCountdown = (seconds: number) => {
  const normalized = Math.max(0, Math.floor(seconds))
  const hours = Math.floor(normalized / 3600)
  const minutes = Math.floor((normalized % 3600) / 60)
  const remainingSeconds = normalized % 60
  return [hours, minutes, remainingSeconds].map((value) => value.toString().padStart(2, '0')).join(':')
}

const getOptionIcon = (name: string) => {
  const normalized = name.trim()
  if (normalized.includes('家具')) return Sofa
  if (normalized.includes('電子')) return MonitorSmartphone
  if (normalized.includes('服飾')) return Shirt
  if (normalized.includes('書籍')) return BookText
  if (normalized.includes('運動')) return Dumbbell
  if (normalized.includes('玩具')) return Gamepad2
  if (normalized.includes('廚房')) return UtensilsCrossed
  if (normalized.includes('生活')) return Home
  if (normalized.includes('嬰幼兒')) return Baby
  return Package
}

const QuickFilterLottieIcon = ({
  filterKey,
  activeKey,
  playNonce,
  fallbackIcon,
}: {
  filterKey: QuickFilterKey
  activeKey: QuickFilterKey | null
  playNonce: number
  fallbackIcon: ReactNode
}) => (
  <span className="quick-filter-icon" aria-hidden="true">
    <span className={`quick-filter-icon-static ${activeKey === filterKey ? 'is-hidden' : ''}`}>{fallbackIcon}</span>
    {activeKey === filterKey ? (
      <span className="quick-filter-icon-lottie">
        <DotLottieReact
          key={`${filterKey}-${playNonce}`}
          src={QUICK_FILTER_LOTTIE[filterKey]}
          autoplay
          loop={false}
          style={{ width: '2.6rem', height: '2.6rem' }}
        />
      </span>
    ) : null}
  </span>
)

export const ListingHomePage = () => {
  const navigate = useNavigate()
  const { isAuthenticated, tokens } = useAuth()
  const [items, setItems] = useState<ListingItem[]>([])
  const [categories, setCategories] = useState<LookupItem[]>([])
  const [conditions, setConditions] = useState<LookupItem[]>([])
  const [residences, setResidences] = useState<LookupItem[]>([])
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
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
  const [loading, setLoading] = useState(false)
  const [showLoadingSkeleton, setShowLoadingSkeleton] = useState(false)
  const [favoriteStateById, setFavoriteStateById] = useState<
    Record<ListingItem['id'], { isFavorited: boolean; favoriteCount: number }>
  >({})
  const [favoriteBusyIds, setFavoriteBusyIds] = useState<Set<ListingItem['id']>>(() => new Set())
  const [conversationBusyIds, setConversationBusyIds] = useState<Set<ListingItem['id']>>(() => new Set())
  const [purchaseBusyIds, setPurchaseBusyIds] = useState<Set<ListingItem['id']>>(() => new Set())
  const [purchaseConfirmTarget, setPurchaseConfirmTarget] = useState<ListingItem | null>(null)
  const [unreadMessageCount, setUnreadMessageCount] = useState(0)
  const [countdownNowMs, setCountdownNowMs] = useState(() => Date.now())
  const [topPinTargetId, setTopPinTargetId] = useState<string | null>(null)
  const [topPinSkipIntro, setTopPinSkipIntro] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const listingSectionRef = useRef<HTMLElement | null>(null)
  const desktopFilterRowRef = useRef<HTMLDivElement | null>(null)
  const desktopFilterAreaRef = useRef<HTMLDivElement | null>(null)
  const quickFilterHoverTimerRef = useRef<number | null>(null)
  const marqueeRef = useRef<HTMLElement | null>(null)
  const marqueePointerXRef = useRef<number | null>(null)

  useEffect(() => {
    setTopPinSkipIntro(window.localStorage.getItem(TOP_PIN_SKIP_INTRO_STORAGE_KEY) === '1')
  }, [])

  useEffect(() => {
    let disposed = false

    void Promise.all([lookupApi.categories(), lookupApi.conditions(), lookupApi.residences()])
      .then(([categoryResult, conditionResult, residenceResult]) => {
        if (disposed) {
          return
        }

        setCategories(categoryResult)
        setConditions(conditionResult)
        setResidences(residenceResult)
      })
      .catch(() => {
        if (!disposed) {
          setError('載入篩選條件失敗，請稍後再試')
        }
      })

    return () => {
      disposed = true
    }
  }, [])

  useEffect(() => {
    let disposed = false
    const startedAt = Date.now()
    let skeletonDelayTimer: number | null = null
    setLoading(true)
    setShowLoadingSkeleton(false)
    setError(null)

    skeletonDelayTimer = window.setTimeout(() => {
      if (!disposed) {
        setShowLoadingSkeleton(true)
      }
    }, 150)

    void listingApi
      .list({
        page,
        pageSize: PAGE_SIZE,
        categoryCodes: selectedCategoryCodes,
        conditionCodes: selectedConditionCodes,
        residenceCodes: selectedResidenceCodes,
        isFree: isFree || undefined,
        isCharity: isCharity || undefined,
        isTradeable: isTradeable || undefined,
      })
      .then((data) => {
        if (disposed) {
          return
        }

        setItems(data.items)
        setTotalPages(data.pagination.totalPages || 1)
      })
      .catch((err: unknown) => {
        if (disposed) {
          return
        }

        const message = err instanceof ApiClientError ? err.message : '讀取商品列表失敗'
        setError(message)
      })
      .finally(() => {
        const elapsed = Date.now() - startedAt
        const wait = Math.max(0, MIN_SKELETON_MS - elapsed)
        window.setTimeout(() => {
          if (!disposed) {
            setLoading(false)
            setShowLoadingSkeleton(false)
          }
        }, wait)
      })

    return () => {
      disposed = true
      if (skeletonDelayTimer !== null) {
        window.clearTimeout(skeletonDelayTimer)
      }
    }
  }, [
    page,
    selectedCategoryCodes,
    selectedConditionCodes,
    selectedResidenceCodes,
    isFree,
    isCharity,
    isTradeable,
  ])

  useEffect(() => {
    setFavoriteStateById((current) => {
      const next: Record<ListingItem['id'], { isFavorited: boolean; favoriteCount: number }> = {}
      items.forEach((item) => {
        const existing = current[item.id]
        next[item.id] = {
          isFavorited: existing?.isFavorited ?? false,
          favoriteCount: existing?.favoriteCount ?? item.interestCount,
        }
      })
      return next
    })
  }, [items])

  useEffect(() => {
    if (!isAuthenticated || !items.length) {
      return
    }

    let disposed = false

    void Promise.all(
      items.map(async (item) => {
        try {
          const status = await listingApi.getFavoriteStatus(item.id)
          return { itemId: item.id, status }
        } catch {
          return null
        }
      }),
    ).then((result) => {
      if (disposed) {
        return
      }

      setFavoriteStateById((current) => {
        const next = { ...current }
        result.forEach((entry) => {
          if (!entry) {
            return
          }

          next[entry.itemId] = {
            isFavorited: entry.status.isFavorited,
            favoriteCount: entry.status.favoriteCount,
          }
        })
        return next
      })
    })

    return () => {
      disposed = true
    }
  }, [isAuthenticated, items])

  useEffect(() => {
    if (!isAuthenticated) {
      setUnreadMessageCount(0)
      return
    }

    let disposed = false
    void messagingApi
      .listConversations()
      .then((conversations) => {
        if (disposed) {
          return
        }
        const totalUnread = conversations.reduce((sum, conversation) => sum + Math.max(0, conversation.unreadCount), 0)
        setUnreadMessageCount(totalUnread)
      })
      .catch(() => {
        if (!disposed) {
          setUnreadMessageCount(0)
        }
      })

    return () => {
      disposed = true
    }
  }, [isAuthenticated, tokens?.userId])

  useEffect(() => {
    const hasPendingCountdown = items.some(
      (item) =>
        Boolean(item.pendingPurchaseRequestExpireAt) &&
        (item.pendingPurchaseRequestRemainingSeconds ?? 0) > 0,
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

  const toggleMultiCode = (
    value: number,
    setValues: Dispatch<SetStateAction<number[]>>,
  ) => {
    setPage(1)
    setValues((current) =>
      current.includes(value) ? current.filter((x) => x !== value) : [...current, value],
    )
  }

  const activeFilterCount =
    selectedCategoryCodes.length +
    selectedConditionCodes.length +
    selectedResidenceCodes.length +
    Number(isFree) +
    Number(isCharity) +
    Number(isTradeable)

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
    const targetTop = listingSectionRef.current?.getBoundingClientRect().top
    if (targetTop == null) {
      return
    }

    const startY = window.scrollY
    const NAV_OFFSET_PX = 104
    const destinationY = startY + targetTop - NAV_OFFSET_PX
    const distance = destinationY - startY
    const duration = 1500
    const startedAt = performance.now()

    const easeInOutCubic = (progress: number) =>
      progress < 0.5 ? 4 * progress * progress * progress : 1 - Math.pow(-2 * progress + 2, 3) / 2

    const animateScroll = (now: number) => {
      const elapsed = now - startedAt
      const progress = Math.min(elapsed / duration, 1)
      const easedProgress = easeInOutCubic(progress)

      window.scrollTo({
        top: startY + distance * easedProgress,
        behavior: 'auto',
      })

      if (progress < 1) {
        window.requestAnimationFrame(animateScroll)
      }
    }

    window.requestAnimationFrame(animateScroll)
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

  const toggleFavorite = async (item: ListingItem) => {
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
  }

  const startConversation = async (item: ListingItem) => {
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
  }

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

  const openPurchaseConfirm = (item: ListingItem) => {
    if (!isAuthenticated) {
      navigate('/login')
      return
    }

    if (tokens?.userId === item.seller.id) {
      setError('這是你的商品，無法購買自己的商品')
      return
    }

    setPurchaseConfirmTarget(item)
  }

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

  const openTopPinFlow = (item: ListingItem) => {
    if (!isAuthenticated) {
      navigate('/login')
      return
    }

    if (!topPinSkipIntro) {
      setTopPinTargetId(item.id)
      return
    }

    navigateToTopPinSection(item.id)
  }

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

  const supportsDesktopHover = () => window.matchMedia('(hover: hover) and (pointer: fine)').matches

  const updateMarqueeFisheye = (clientX: number) => {
    const container = marqueeRef.current
    if (!container) {
      return
    }

    const radius = 190
    const maxScaleBoost = 0.55
    const maxGapEm = 0.14
    const chars = container.querySelectorAll<HTMLElement>('.marquee-char')
    chars.forEach((char) => {
      const rect = char.getBoundingClientRect()
      const centerX = rect.left + rect.width / 2
      const distance = Math.abs(centerX - clientX)
      const normalized = Math.max(0, 1 - distance / radius)
      const scale = 1 + normalized * maxScaleBoost
      const gap = normalized * maxGapEm
      char.style.setProperty('--char-scale', scale.toFixed(3))
      char.style.setProperty('--char-gap', `${gap.toFixed(3)}em`)
    })
  }

  const handleMarqueeMouseEnter = (event: React.MouseEvent<HTMLElement>) => {
    if (!supportsDesktopHover()) {
      return
    }

    marqueePointerXRef.current = event.clientX
    updateMarqueeFisheye(event.clientX)
  }

  const handleMarqueeMouseMove = (event: React.MouseEvent<HTMLElement>) => {
    marqueePointerXRef.current = event.clientX

    if (!supportsDesktopHover()) {
      return
    }

    updateMarqueeFisheye(event.clientX)
  }

  const handleMarqueeMouseLeave = () => {
    marqueePointerXRef.current = null

    const container = marqueeRef.current
    if (!container) {
      return
    }

    const chars = container.querySelectorAll<HTMLElement>('.marquee-char')
    chars.forEach((char) => {
      char.style.setProperty('--char-scale', '1')
      char.style.setProperty('--char-gap', '0em')
    })
  }

  const renderMarqueeText = (text: string, keyPrefix: string) => (
    <span className="marquee-divider-item">
      {Array.from(text).map((char, index) => (
        <span key={`${keyPrefix}-${index}`} className="marquee-char" aria-hidden="true">
          {char === ' ' ? '\u00A0' : char}
        </span>
      ))}
    </span>
  )

  const renderMultiOptions = (
    options: LookupItem[],
    selected: number[],
    onToggle: (code: number) => void,
    withIcon = false,
  ) => (
    <section className="space-y-3">
      <div className="grid grid-cols-2 gap-2">
        {options.map((option, index) => {
          const active = selected.includes(option.id)
          return (
            <Button
              key={option.id}
              type="button"
              onClick={() => onToggle(option.id)}
              variant="secondary"
              className={`animate-fade-in min-h-[3.6rem] w-full rounded-xl border px-3 py-2 text-xl font-semibold transition focus-visible:outline-none ${
                active
                  ? '!border-[#B08F68] !bg-[#D6B897] !text-text-main shadow-[0_4px_10px_rgba(37,25,16,0.18)] hover:!bg-[#CCAB87]'
                  : 'border-border bg-surface text-text-main hover:bg-surface-2'
              }`}
              style={{ animationDelay: `${index * 45}ms` }}
            >
              {withIcon ? (
                <span className="inline-flex items-center gap-2.5">
                  {(() => {
                    const OptionIcon = getOptionIcon(option.displayName)
                    return <OptionIcon className="h-5 w-5" aria-hidden="true" />
                  })()}
                  {option.displayName}
                </span>
              ) : (
                option.displayName
              )}
            </Button>
          )
        })}
      </div>
    </section>
  )

  const getFilterSummary = (title: string, selectedCodes: number[], options: LookupItem[]) => {
    if (!selectedCodes.length) {
      return title
    }

    const selectedNames = selectedCodes
      .map((code) => options.find((option) => option.id === code)?.displayName)
      .filter((name): name is string => Boolean(name))

    if (!selectedNames.length) {
      return title
    }

    if (selectedNames.length <= 2) {
      return selectedNames.join('、')
    }

    return `${selectedNames.slice(0, 2).join('、')} +${selectedNames.length - 2}`
  }

  const desktopFilterGroups: {
    key: ExpandableFilterKey
    title: string
    options: LookupItem[]
    selected: number[]
    setValues: Dispatch<SetStateAction<number[]>>
  }[] = [
    {
      key: 'category',
      title: '分類',
      options: categories,
      selected: selectedCategoryCodes,
      setValues: setSelectedCategoryCodes,
    },
    {
      key: 'condition',
      title: '品況',
      options: conditions,
      selected: selectedConditionCodes,
      setValues: setSelectedConditionCodes,
    },
    {
      key: 'residence',
      title: '社宅',
      options: residences,
      selected: selectedResidenceCodes,
      setValues: setSelectedResidenceCodes,
    },
  ]
  const expandedDesktopGroup = desktopFilterGroups.find((group) => group.key === expandedFilter) ?? null
  const mobileCategorySummary = getFilterSummary('分類', selectedCategoryCodes, categories)
  const mobileConditionSummary = getFilterSummary('品況', selectedConditionCodes, conditions)
  const mobileResidenceSummary = getFilterSummary('社宅', selectedResidenceCodes, residences)
  const mobileCategoryActive = mobileSheetFilter === 'category' || selectedCategoryCodes.length > 0
  const mobileConditionActive = mobileSheetFilter === 'condition' || selectedConditionCodes.length > 0
  const mobileResidenceActive = mobileSheetFilter === 'residence' || selectedResidenceCodes.length > 0

  return (
    <main className="mx-auto flex w-full max-w-6xl flex-col px-4 py-6 md:py-8">
      <section className="order-2 mb-0 flex min-h-[62vh] flex-col items-center justify-center space-y-3 text-center md:order-1 md:min-h-[75vh]">
        <p className="animate-fade-in text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1
          className="animate-fade-in inline-block text-6xl font-semibold leading-[1.03] text-text-main sm:text-7xl md:text-8xl"
          style={{ animationDelay: '160ms' }}
        >
          <span className="block">
            社宅<span className="marker-wipe">專屬</span>
          </span>
          <span className="block">二手交易平台</span>
        </h1>
        <div
          className="animate-fade-in mt-6 grid w-full max-w-[24rem] grid-cols-2 gap-3 md:flex md:max-w-none md:flex-wrap md:items-center md:justify-center md:gap-4"
          style={{ animationDelay: '780ms' }}
        >
          <motion.div
            initial={{ opacity: 0, y: 28, scale: 0.96 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            transition={{ duration: 0.7, delay: 0.75, ease: [0.22, 1, 0.36, 1] }}
          >
            <Button
              type="button"
              className="text-swap-trigger !h-[3.7rem] !w-full rounded-2xl !px-0 !py-0 !text-[1.45rem] !font-semibold shadow-soft md:!h-[4.6rem] md:!w-[16rem] md:!text-2xl"
              onClick={handleBrowseListings}
            >
              <span className="btn-text-swap">
                <span className="btn-text-swap-primary">瀏覽商品</span>
                <span className="btn-text-swap-secondary">
                  <span className="btn-text-icon" aria-hidden="true">
                    ↓
                  </span>
                  立即瀏覽
                </span>
              </span>
            </Button>
          </motion.div>
          <motion.div
            initial={{ opacity: 0, y: 28, scale: 0.96 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            transition={{ duration: 0.7, delay: 0.84, ease: [0.22, 1, 0.36, 1] }}
          >
            <Link
              to="/listings/create"
              className={getButtonClassName({
                className:
                  'inline-flex h-[3.7rem] w-full items-center justify-center rounded-2xl px-0 py-0 text-[1.45rem] font-semibold md:h-[4.6rem] md:w-[16rem] md:text-2xl',
              })}
            >
              刊登商品
            </Link>
          </motion.div>
          <motion.div
            className="col-span-2 md:col-span-1"
            initial={{ opacity: 0, y: 28, scale: 0.96 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            transition={{ duration: 0.7, delay: 0.96, ease: [0.22, 1, 0.36, 1] }}
          >
            <Link
              to="/messages"
              className={getButtonClassName({
                variant: 'secondary',
                className:
                  'text-swap-trigger relative inline-flex h-[3.7rem] w-full items-center justify-center rounded-2xl px-0 py-0 text-[1.45rem] font-semibold md:h-[4.6rem] md:w-[16rem] md:text-2xl',
              })}
            >
              <span className="btn-text-swap">
                <span className="btn-text-swap-primary">我的訊息</span>
                <span className="btn-text-swap-secondary">
                  <span className="btn-text-icon" aria-hidden="true">
                    ✉
                  </span>
                  前往聊天室
                </span>
              </span>
              {unreadMessageCount > 0 ? (
                <span className="absolute right-2 top-2 inline-flex min-h-6 min-w-6 items-center justify-center rounded-full bg-[#D64545] px-1.5 text-xs font-bold text-white md:right-3 md:top-3 md:text-sm">
                  {unreadMessageCount > 99 ? '99+' : unreadMessageCount}
                </span>
              ) : null}
            </Link>
          </motion.div>
        </div>
      </section>

      <motion.section
        ref={marqueeRef}
        className="marquee-divider order-1 h-auto min-h-[3.75rem] md:order-2 md:h-[75vh]"
        aria-label="交易安全提醒"
        initial={{ opacity: 0, y: 90 }}
        whileInView={{ opacity: 1, y: 0 }}
        viewport={{ once: false, amount: 0.42 }}
        transition={{ duration: 0.85, ease: [0.22, 1, 0.36, 1] }}
        onMouseEnter={handleMarqueeMouseEnter}
        onMouseMove={handleMarqueeMouseMove}
        onMouseLeave={handleMarqueeMouseLeave}
      >
        <div className="marquee-divider-track">
          {renderMarqueeText(MARQUEE_NOTICE_TEXT, 'notice-a')}
          <span aria-hidden="true">{renderMarqueeText(MARQUEE_NOTICE_TEXT, 'notice-b')}</span>
        </div>
      </motion.section>

      <div className="order-3 md:order-3">
      <section ref={listingSectionRef} aria-label="商品篩選起點" className="h-0 scroll-mt-28" />
      <section className="animate-fade-rise mb-6 space-y-4" style={{ animationDelay: '360ms' }}>
        <div ref={desktopFilterAreaRef} className="hidden space-y-4 md:block">
          <div ref={desktopFilterRowRef} className="flex items-start justify-between gap-5 md:flex-nowrap">
            <div className="grid flex-1 grid-cols-3 items-start gap-5">
              {desktopFilterGroups.map((group) => {
                const isExpanded = expandedFilter === group.key
                const hasSelectedValue = group.selected.length > 0
                const flyInClass =
                  group.key === 'category'
                    ? 'fi-1'
                    : group.key === 'condition'
                      ? 'fi-2'
                      : 'fi-3'
                return (
                  <div key={group.key} className="min-w-0">
                    <Button
                      type="button"
                      variant="secondary"
                      className={`filter-trigger filter-fly-in ${flyInClass} ${filtersInView ? 'is-visible' : ''} flex h-[4.2rem] w-full items-center justify-center rounded-[999px] border px-6 text-[1.3rem] font-semibold shadow-soft transition ${
                        isExpanded || hasSelectedValue
                          ? '!border-[#B08F68] !bg-[#D6B897] !text-text-main shadow-[0_4px_10px_rgba(37,25,16,0.18)] hover:!bg-[#CCAB87]'
                          : 'border-border bg-surface text-text-main hover:bg-surface-2'
                      }`}
                      onClick={() => setExpandedFilter((current) => (current === group.key ? null : group.key))}
                    >
                      <span className="filter-trigger-icon" aria-hidden="true">
                        <svg viewBox="0 0 24 24" className="h-7 w-7" fill="none" stroke="currentColor" strokeWidth="2.2">
                          <circle cx="11" cy="11" r="6.5" />
                          <path d="M16 16L21 21" strokeLinecap="round" />
                        </svg>
                      </span>
                      <span className="filter-trigger-label truncate">
                        {getFilterSummary(group.title, group.selected, group.options)}
                      </span>
                    </Button>
                  </div>
                )
              })}
            </div>

            <div className="flex shrink-0 flex-nowrap justify-end gap-5">
            <Button
              type="button"
              onMouseEnter={() => triggerQuickFilterLottie('free')}
              onFocus={() => triggerQuickFilterLottie('free')}
              onMouseLeave={clearQuickFilterLottie}
              onBlur={clearQuickFilterLottie}
              onClick={() => {
                setPage(1)
                setIsFree((current) => !current)
              }}
              variant="secondary"
              className={`filter-fly-in fi-4 ${filtersInView ? 'is-visible' : ''} h-[4.2rem] min-w-[8.6rem] rounded-[9999px] border px-6 text-[1.3rem] font-semibold shadow-soft transition active:scale-[0.98] ${
                isFree
                  ? '!border-[#2F7D4E] !bg-[#2F7D4E] !text-white hover:!bg-[#276942]'
                  : 'border-border bg-surface text-text-main hover:bg-surface-2'
              }`}
            >
              <span className="inline-flex items-center gap-2">
                <QuickFilterLottieIcon
                  filterKey="free"
                  activeKey={quickFilterHover}
                  playNonce={quickFilterPlayNonce}
                  fallbackIcon={
                    <svg
                      viewBox="0 0 24 24"
                      className="h-7 w-7"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="2"
                    >
                      <rect x="4" y="9" width="16" height="11" rx="2" />
                      <path d="M12 9V20M4 13H20M7.5 9C6.3 9 5.5 8.3 5.5 7.3C5.5 6.1 6.5 5.4 7.5 5.4C9.4 5.4 10.8 7.2 12 9M16.5 9C17.7 9 18.5 8.3 18.5 7.3C18.5 6.1 17.5 5.4 16.5 5.4C14.6 5.4 13.2 7.2 12 9" />
                    </svg>
                  }
                />
                免費
              </span>
            </Button>
            <Button
              type="button"
              onMouseEnter={() => triggerQuickFilterLottie('charity')}
              onFocus={() => triggerQuickFilterLottie('charity')}
              onMouseLeave={clearQuickFilterLottie}
              onBlur={clearQuickFilterLottie}
              onClick={() => {
                setPage(1)
                setIsCharity((current) => !current)
              }}
              variant="secondary"
              className={`filter-fly-in fi-5 ${filtersInView ? 'is-visible' : ''} h-[4.2rem] min-w-[8.6rem] rounded-[9999px] border px-6 text-[1.3rem] font-semibold shadow-soft transition active:scale-[0.98] ${
                isCharity
                  ? '!border-[#B45B4D] !bg-[#B45B4D] !text-white hover:!bg-[#984B40]'
                  : 'border-border bg-surface text-text-main hover:bg-surface-2'
              }`}
            >
              <span className="inline-flex items-center gap-2">
                <QuickFilterLottieIcon
                  filterKey="charity"
                  activeKey={quickFilterHover}
                  playNonce={quickFilterPlayNonce}
                  fallbackIcon={
                    <svg
                      viewBox="0 0 24 24"
                      className="h-7 w-7"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="2"
                    >
                      <path d="M12 20.4C11.2 19.7 4.5 14.2 4.5 9.4C4.5 7.1 6.3 5.3 8.6 5.3C10 5.3 11.2 6 12 7.1C12.8 6 14 5.3 15.4 5.3C17.7 5.3 19.5 7.1 19.5 9.4C19.5 14.2 12.8 19.7 12 20.4Z" />
                    </svg>
                  }
                />
                愛心捐贈
              </span>
            </Button>
            <Button
              type="button"
              onMouseEnter={() => triggerQuickFilterLottie('tradeable')}
              onFocus={() => triggerQuickFilterLottie('tradeable')}
              onMouseLeave={clearQuickFilterLottie}
              onBlur={clearQuickFilterLottie}
              onClick={() => {
                setPage(1)
                setIsTradeable((current) => !current)
              }}
              variant="secondary"
              className={`filter-fly-in fi-6 ${filtersInView ? 'is-visible' : ''} h-[4.2rem] min-w-[8.6rem] rounded-[9999px] border px-6 text-[1.3rem] font-semibold shadow-soft transition active:scale-[0.98] ${
                isTradeable
                  ? '!border-[#5E5AB5] !bg-[#5E5AB5] !text-white hover:!bg-[#4F4B99]'
                  : 'border-border bg-surface text-text-main hover:bg-surface-2'
              }`}
            >
              <span className="inline-flex items-center gap-2">
                <QuickFilterLottieIcon
                  filterKey="tradeable"
                  activeKey={quickFilterHover}
                  playNonce={quickFilterPlayNonce}
                  fallbackIcon={
                    <svg
                      viewBox="0 0 24 24"
                      className="h-7 w-7"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="2"
                    >
                      <path d="M6 8H18M14.5 4.5L18 8L14.5 11.5M18 16H6M9.5 12.5L6 16L9.5 19.5" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  }
                />
                以物易物
              </span>
            </Button>
            <Button
              type="button"
              variant="secondary"
              onClick={clearAllFilters}
              className={`filter-fly-in fi-7 ${filtersInView ? 'is-visible' : ''} h-[4.2rem] rounded-[999px] px-6 text-[1.3rem] font-semibold whitespace-nowrap`}
            >
              清除條件 {activeFilterCount ? `(${activeFilterCount})` : ''}
            </Button>
          </div>
        </div>
          <div
            className={`overflow-hidden transition-all duration-300 ease-out ${
              expandedDesktopGroup ? 'mt-1 max-h-72 overflow-visible opacity-100' : 'max-h-0 overflow-hidden opacity-0'
            }`}
          >
            {expandedDesktopGroup ? (
              <section>
                <div className="hide-scrollbar flex items-center gap-3 overflow-x-auto overflow-y-visible py-2">
                  {expandedDesktopGroup.options.map((option, index) => {
                    const active = expandedDesktopGroup.selected.includes(option.id)
                    return (
                      <Button
                        key={option.id}
                        type="button"
                        onClick={() => toggleMultiCode(option.id, expandedDesktopGroup.setValues)}
                        variant="secondary"
                        className={`option-chip-enter shrink-0 rounded-2xl border px-6 py-3 text-[1.2rem] font-semibold transition focus-visible:outline-none ${
                          active
                            ? '!border-[#B08F68] !bg-[#D6B897] !text-text-main shadow-[0_4px_10px_rgba(37,25,16,0.18)] hover:!bg-[#CCAB87]'
                            : 'border-border bg-surface text-text-main hover:bg-surface-2'
                        }`}
                        style={{ animationDelay: `${index * 55}ms` }}
                      >
                        {expandedDesktopGroup.key === 'category' ? (
                          <span className="inline-flex items-center gap-2.5">
                            {(() => {
                              const OptionIcon = getOptionIcon(option.displayName)
                              return <OptionIcon className="h-5 w-5" aria-hidden="true" />
                            })()}
                            {option.displayName}
                          </span>
                        ) : (
                          option.displayName
                        )}
                      </Button>
                    )
                  })}
                </div>
              </section>
            ) : null}
          </div>
        </div>

        <div className="grid grid-cols-3 gap-2 md:hidden">
          <Button
            type="button"
            variant="secondary"
            className={`h-14 rounded-[999px] px-2 text-xl font-semibold shadow-soft ${
              mobileCategoryActive
                ? '!border-brand !bg-brand !text-brand-foreground hover:!bg-brand-strong'
                : 'border-border bg-surface text-text-main hover:bg-surface-2'
            }`}
            onClick={() => setMobileSheetFilter('category')}
          >
            <span className="block truncate px-1">{mobileCategorySummary}</span>
          </Button>
          <Button
            type="button"
            variant="secondary"
            className={`h-14 rounded-[999px] px-2 text-xl font-semibold shadow-soft ${
              mobileConditionActive
                ? '!border-brand !bg-brand !text-brand-foreground hover:!bg-brand-strong'
                : 'border-border bg-surface text-text-main hover:bg-surface-2'
            }`}
            onClick={() => setMobileSheetFilter('condition')}
          >
            <span className="block truncate px-1">{mobileConditionSummary}</span>
          </Button>
          <Button
            type="button"
            variant="secondary"
            className={`h-14 rounded-[999px] px-2 text-xl font-semibold shadow-soft ${
              mobileResidenceActive
                ? '!border-brand !bg-brand !text-brand-foreground hover:!bg-brand-strong'
                : 'border-border bg-surface text-text-main hover:bg-surface-2'
            }`}
            onClick={() => setMobileSheetFilter('residence')}
          >
            <span className="block truncate px-1">{mobileResidenceSummary}</span>
          </Button>
        </div>

        <div className="grid grid-cols-3 gap-2 md:hidden">
          <Button
            type="button"
            variant="secondary"
            onClick={() => {
              setPage(1)
              setIsFree((current) => !current)
            }}
            className={`h-14 rounded-[999px] border px-2 text-xl font-semibold whitespace-nowrap shadow-soft transition active:scale-[0.98] ${
              isFree
                ? '!border-[#2F7D4E] !bg-[#2F7D4E] !text-white hover:!bg-[#276942]'
                : 'border-border bg-surface text-text-main'
            }`}
          >
            免費
          </Button>
          <Button
            type="button"
            variant="secondary"
            onClick={() => {
              setPage(1)
              setIsCharity((current) => !current)
            }}
            className={`h-14 rounded-[999px] border px-2 text-xl font-semibold whitespace-nowrap shadow-soft transition active:scale-[0.98] ${
              isCharity
                ? '!border-[#B45B4D] !bg-[#B45B4D] !text-white hover:!bg-[#984B40]'
                : 'border-border bg-surface text-text-main'
            }`}
          >
            愛心捐贈
          </Button>
          <Button
            type="button"
            variant="secondary"
            onClick={() => {
              setPage(1)
              setIsTradeable((current) => !current)
            }}
            className={`h-14 rounded-[999px] border px-2 text-xl font-semibold whitespace-nowrap shadow-soft transition active:scale-[0.98] ${
              isTradeable
                ? '!border-[#5E5AB5] !bg-[#5E5AB5] !text-white hover:!bg-[#4F4B99]'
                : 'border-border bg-surface text-text-main'
            }`}
          >
            以物易物
          </Button>
          <Button
            type="button"
            variant="secondary"
            onClick={clearAllFilters}
            className="col-span-3 h-14 rounded-[999px] text-xl font-semibold"
          >
            清除條件 {activeFilterCount ? `(${activeFilterCount})` : ''}
          </Button>
        </div>
      </section>

      {mobileSheetFilter ? (
        <div className="fixed inset-0 z-20 flex items-end bg-black/35 pb-3 md:hidden">
          <button
            type="button"
            className="absolute inset-0"
            aria-label="關閉條件選單"
            onClick={() => setMobileSheetFilter(null)}
          />
          <Card className="animate-sheet-up relative z-10 mx-auto flex h-[62vh] w-[calc(100%-1rem)] flex-col overflow-auto rounded-2xl p-0">
            <div className="sticky top-0 z-10 mb-4 flex items-center justify-between rounded-t-2xl border-b border-border bg-[#F3E7D8] px-4 py-3">
              <h3 className="text-3xl font-semibold text-text-main">
                {mobileSheetFilter === 'category'
                  ? '選擇分類'
                  : mobileSheetFilter === 'condition'
                    ? '選擇品況'
                    : '選擇社宅'}
              </h3>
              <Button
                type="button"
                variant="secondary"
                className="min-h-[3.3rem] px-5 text-xl font-semibold"
                onClick={() => setMobileSheetFilter(null)}
              >
                完成
              </Button>
            </div>
            <div className="px-3 pb-4">
              {mobileSheetFilter === 'category'
                ? renderMultiOptions(categories, selectedCategoryCodes, (code) =>
                    toggleMultiCode(code, setSelectedCategoryCodes),
                  true,
                )
                : null}
              {mobileSheetFilter === 'condition'
                ? renderMultiOptions(conditions, selectedConditionCodes, (code) =>
                    toggleMultiCode(code, setSelectedConditionCodes),
                  )
                : null}
              {mobileSheetFilter === 'residence'
                ? renderMultiOptions(residences, selectedResidenceCodes, (code) =>
                    toggleMultiCode(code, setSelectedResidenceCodes),
                  )
                : null}
            </div>
          </Card>
        </div>
      ) : null}

      {error ? <p className="mb-4 text-sm text-danger">{error}</p> : null}

      {showLoadingSkeleton ? (
        <div className="grid grid-cols-2 gap-x-4 gap-y-8 md:grid-cols-3 md:gap-y-8 lg:grid-cols-4 2xl:grid-cols-5">
          {Array.from({ length: 8 }).map((_, index) => (
            <Card key={index} className="h-56 animate-pulse bg-surface-2" />
          ))}
        </div>
      ) : null}

      {!loading && !items.length ? (
        <EmptyState title="目前沒有符合條件的商品" description="請調整篩選條件，或稍後再試一次。" />
      ) : null}

      {items.length && (!loading || !showLoadingSkeleton) ? (
        <>
          <motion.section
            layout
            className={`grid grid-cols-2 gap-x-4 gap-y-8 transition-opacity duration-150 md:grid-cols-3 md:gap-y-8 lg:grid-cols-4 2xl:grid-cols-5 ${
              loading ? 'opacity-70' : 'opacity-100'
            }`}
          >
            <AnimatePresence mode="popLayout">
              {items.map((item) => {
                const favoriteState = favoriteStateById[item.id]
                const isLiked = favoriteState?.isFavorited ?? false
                const displayInterestCount = favoriteState?.favoriteCount ?? item.interestCount
                const favoriteBusy = favoriteBusyIds.has(item.id)
                const isOwnListing = tokens?.userId === item.seller.id
                const conversationBusy = conversationBusyIds.has(item.id)
                const purchaseBusy = purchaseBusyIds.has(item.id)
                const pendingExpireAt = item.pendingPurchaseRequestExpireAt
                const pendingRemainingFromServer = item.pendingPurchaseRequestRemainingSeconds
                const pendingRemainingFromNow =
                  pendingExpireAt === null ? null : Math.max(0, Math.floor((new Date(pendingExpireAt).getTime() - countdownNowMs) / 1000))
                const pendingRemainingSeconds =
                  pendingRemainingFromNow ?? (pendingRemainingFromServer == null ? null : Math.max(0, pendingRemainingFromServer))
                const hasPendingPurchaseRequest = pendingRemainingSeconds != null && pendingRemainingSeconds > 0

                return (
                <motion.div
                  key={item.id}
                  layout
                  initial={{ opacity: 0, y: 24, scale: 0.97 }}
                  animate={{ opacity: 1, y: 0, scale: 1 }}
                  exit={{ opacity: 0, y: -14, scale: 0.96 }}
                  transition={{
                    duration: 0.36,
                    ease: [0.22, 1, 0.36, 1],
                    layout: {
                      duration: 0.42,
                      delay: 0.15,
                      ease: [0.22, 1, 0.36, 1],
                    },
                  }}
                >
                  <div className="flex h-full flex-col gap-2">
                    <div className="flex h-full flex-col overflow-hidden rounded-2xl border border-border bg-surface shadow-soft">
                      <div className="relative aspect-square overflow-hidden">
                      {item.isPinned ? (
                        <div className="absolute left-2 top-2 z-10">
                          <span className="rounded-full bg-[#D64545] px-2.5 py-1 text-xs font-semibold text-white">
                            置頂中
                          </span>
                        </div>
                      ) : null}
                      <div className="absolute right-2 top-2 z-10">
                        <span className="rounded-full bg-black/70 px-3 py-1 text-sm font-semibold text-white">
                          {item.categoryName}
                        </span>
                      </div>
                      <Link to={`/listings/${item.id}?from=listings`} className="block h-full w-full" aria-label={`查看商品：${item.title}`}>
                        {item.mainImageUrl ? (
                          <img src={item.mainImageUrl} alt={item.title} className="h-full w-full object-cover" />
                        ) : (
                          <div className="flex h-full items-center justify-center text-sm text-text-muted">無圖片</div>
                        )}
                      </Link>
                      {hasPendingPurchaseRequest ? (
                        <div className="absolute inset-0 z-20 flex flex-col items-center justify-center gap-1 bg-black/55 px-3 text-center text-white">
                          <p className="text-sm font-semibold tracking-wide">交易處理中</p>
                          <p className="text-xl font-bold tabular-nums">{formatCountdown(pendingRemainingSeconds)}</p>
                        </div>
                      ) : null}
                    </div>

                    <div className="space-y-1 px-4 pb-4 pt-4">
                      <>
                            <p className="text-xs font-medium tracking-wide text-text-muted">{item.conditionName}</p>
                            <Link
                              to={`/listings/${item.id}?from=listings`}
                              className="block truncate text-lg font-semibold text-text-main underline-offset-2 hover:underline"
                            >
                              {item.title}
                            </Link>
                            <div className="flex items-center justify-between">
                              <span className={`text-lg font-semibold ${item.isFree ? 'text-[#3C8A65]' : 'text-text-subtle'}`}>
                                {formatPrice(item)}
                              </span>
                              {!isOwnListing ? (
                                <button
                                  type="button"
                                  onClick={() => void toggleFavorite(item)}
                                  disabled={favoriteBusy}
                                  className="inline-flex items-center gap-1 rounded-full px-1 py-0.5 text-text-muted transition hover:text-[#B45B4D] focus-visible:outline-none"
                                  aria-label={isLiked ? '取消收藏' : '加入收藏'}
                                >
                                  <svg
                                    viewBox="0 0 24 24"
                                    className={`h-7 w-7 transition-all duration-200 ${isLiked ? 'scale-110 text-[#B45B4D]' : 'text-text-muted'}`}
                                    fill={isLiked ? 'currentColor' : 'none'}
                                    stroke="currentColor"
                                    strokeWidth="2"
                                  >
                                    <path d="M12 20.4C11.2 19.7 4.5 14.2 4.5 9.4C4.5 7.1 6.3 5.3 8.6 5.3C10 5.3 11.2 6 12 7.1C12.8 6 14 5.3 15.4 5.3C17.7 5.3 19.5 7.1 19.5 9.4C19.5 14.2 12.8 19.7 12 20.4Z" />
                                  </svg>
                                  <span className={`text-lg font-semibold transition-colors duration-200 ${isLiked ? 'text-[#B45B4D]' : 'text-text-muted'}`}>
                                    {displayInterestCount}
                                  </span>
                                </button>
                              ) : (
                                <button
                                  type="button"
                                  onClick={() => openTopPinFlow(item)}
                                  className="inline-flex items-center justify-center rounded-full p-1.5 text-text-muted transition hover:text-[#B45B4D] focus-visible:outline-none"
                                  aria-label="我要置頂"
                                  title="我要置頂"
                                >
                                  <Rocket className="h-6 w-6" aria-hidden="true" />
                                </button>
                              )}
                            </div>
                            <div className="flex items-center gap-1.5">
                              <Link
                                to={`/seller/${item.seller.id}`}
                                className="shrink-0 truncate text-sm text-text-muted underline-offset-2 hover:text-text-main hover:underline"
                              >
                                {item.seller.displayName || '未提供'}
                              </Link>
                              {item.seller.lineNotifyBound ? (
                                <span className="inline-flex h-6 w-6 items-center justify-center" title="LINE通知已綁定">
                                  <img src={LINE_NOTIFY_ICON} alt="" className="h-5 w-5 object-contain" aria-hidden="true" />
                                  <span className="sr-only">LINE通知已綁定</span>
                                </span>
                              ) : null}
                              {item.seller.emailNotificationEnabled ? (
                                <span className="inline-flex h-6 w-6 items-center justify-center" title="Email通知已開啟">
                                  <img src={EMAIL_NOTIFY_ICON} alt="" className="h-5 w-5 object-contain" aria-hidden="true" />
                                  <span className="sr-only">Email通知已開啟</span>
                                </span>
                              ) : null}
                              {item.seller.quickResponder ? (
                                <span className="inline-flex h-6 w-6 items-center justify-center" title="快速回覆勳章已獲得">
                                  <img src={QUICK_RESPONDER_ICON} alt="" className="h-5 w-5 object-contain" aria-hidden="true" />
                                  <span className="sr-only">快速回覆勳章已獲得</span>
                                </span>
                              ) : null}
                            </div>
                      </>
                    </div>
                    </div>
                    <div className={`grid gap-2 ${isOwnListing ? 'grid-cols-1' : 'grid-cols-2'}`}>
                      <Button
                        type="button"
                        onClick={() => void startConversation(item)}
                        disabled={isOwnListing || conversationBusy}
                        variant="secondary"
                        className="rounded-lg px-2.5 py-1.5 text-lg font-semibold"
                      >
                        {isOwnListing ? '自己的商品' : conversationBusy ? '連線中...' : '聊一下'}
                      </Button>
                      {!isOwnListing ? (
                        <Button
                          type="button"
                          onClick={() => openPurchaseConfirm(item)}
                          disabled={purchaseBusy}
                          className="rounded-lg px-2.5 py-1.5 text-lg font-semibold"
                        >
                          {purchaseBusy ? '處理中...' : '購買'}
                        </Button>
                      ) : null}
                    </div>
                  </div>
                </motion.div>
                )
              })}
            </AnimatePresence>
          </motion.section>
          <footer className="mt-6 flex flex-col items-center justify-between gap-3 sm:flex-row">
            <Button variant="secondary" disabled={page <= 1} onClick={() => setPage((current) => current - 1)}>
              上一頁
            </Button>
            <span className="text-sm text-text-subtle">
              第 {page} / {totalPages} 頁
            </span>
            <Button
              variant="secondary"
              disabled={page >= totalPages}
              onClick={() => setPage((current) => current + 1)}
            >
              下一頁
            </Button>
          </footer>
        </>
      ) : null}
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
