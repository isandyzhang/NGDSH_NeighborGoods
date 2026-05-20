import { useEffect, useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { accountApi } from '@/features/account/api/accountApi'
import { saveLineBindingPending } from '@/features/account/lineBindingSession'
import { listingApi, type ListingDetail } from '@/features/listings/api/listingApi'
import { ListingImageCarousel } from '@/features/listings/components/ListingImageCarousel'
import {
  canEditListing,
  canPurchaseListing,
  canShareListing,
  getListingDetailOverlay,
  getListingStatusLabel,
  getUnavailableBannerMessage,
  shouldShowUnavailableBanner,
} from '@/features/listings/constants/listingStatus'
import { PurchaseConfirmModal } from '@/features/listings/components/PurchaseConfirmModal'
import {
  getLiffShareDiagnostics,
  shareListingToLineFlexOnly,
  shareListingToLine,
  type LiffShareDiagnostics,
  type ShareListingResult,
} from '@/features/listings/utils/lineShare'
import { useAuth } from '@/features/auth/components/AuthProvider'
import { messagingApi } from '@/features/messaging/api/messagingApi'
import { ApiClientError } from '@/shared/types/api'
import { ConfirmModal } from '@/shared/ui/modal/ConfirmModal'
import { Button, getButtonClassName } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { AppModal } from '@/shared/ui/modal/AppModal'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

const formatPrice = (item: ListingDetail) => {
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

const parseApiDateToMs = (value: string) => {
  const hasTimezone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value)
  const normalized = hasTimezone ? value : `${value}Z`
  const parsed = Date.parse(normalized)
  return Number.isNaN(parsed) ? null : parsed
}

export const ListingDetailPage = () => {
  const navigate = useNavigate()
  const { isAuthenticated, tokens } = useAuth()
  const { id = '' } = useParams()
  const [searchParams] = useSearchParams()
  const [countdownNowMs, setCountdownNowMs] = useState(() => Date.now())
  const [conversationBusy, setConversationBusy] = useState(false)
  const [purchaseBusy, setPurchaseBusy] = useState(false)
  const [purchaseConfirmOpen, setPurchaseConfirmOpen] = useState(false)
  const [lineBindPromptOpen, setLineBindPromptOpen] = useState(false)
  const [lineBindBusy, setLineBindBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [shareDebugOpen, setShareDebugOpen] = useState(false)
  const [shareDebugBusy, setShareDebugBusy] = useState(false)
  const [shareDebugInfo, setShareDebugInfo] = useState<LiffShareDiagnostics | null>(null)
  const [shareDebugResult, setShareDebugResult] = useState<ShareListingResult | null>(null)
  const [flexShareBusy, setFlexShareBusy] = useState(false)
  const [isLiffShareEnvReady, setIsLiffShareEnvReady] = useState<boolean | null>(null)

  const detailQuery = useQuery({
    queryKey: ['listings', 'detail', id],
    queryFn: () => listingApi.getById(id),
    enabled: Boolean(id),
    staleTime: 15_000,
    refetchOnWindowFocus: true,
  })

  const item: ListingDetail | null = detailQuery.data ?? null
  const loading = detailQuery.isPending

  const imageSlides = useMemo(() => {
    if (!item) {
      return [] as string[]
    }
    const urls = (item.imageUrls ?? []).filter((u) => u.trim().length > 0)
    if (urls.length > 0) {
      return urls
    }
    if (item.mainImageUrl && item.mainImageUrl.trim().length > 0) {
      return [item.mainImageUrl]
    }
    return []
  }, [item])

  useEffect(() => {
    if (!detailQuery.isError) {
      setError(null)
      return
    }
    const message =
      detailQuery.error instanceof ApiClientError ? detailQuery.error.message : '讀取商品詳情失敗'
    setError(message)
  }, [detailQuery.isError, detailQuery.error])

  useEffect(() => {
    if (!item) {
      return
    }

    const hasPendingByExpireAt = Boolean(item.pendingPurchaseRequestExpireAt)
    const hasPendingByServerRemaining = (item.pendingPurchaseRequestRemainingSeconds ?? 0) > 0
    if (!hasPendingByExpireAt && !hasPendingByServerRemaining) {
      return
    }

    const timer = window.setInterval(() => {
      setCountdownNowMs(Date.now())
    }, 1000)

    return () => {
      window.clearInterval(timer)
    }
  }, [item])

  const pendingExpireAtMs = item?.pendingPurchaseRequestExpireAt
    ? parseApiDateToMs(item.pendingPurchaseRequestExpireAt)
    : null
  const pendingRemainingFromNow =
    pendingExpireAtMs == null ? null : Math.max(0, Math.floor((pendingExpireAtMs - countdownNowMs) / 1000))
  const pendingRemainingFromServer = item?.pendingPurchaseRequestRemainingSeconds ?? null
  const pendingRemainingSeconds =
    pendingRemainingFromNow ?? (pendingRemainingFromServer == null ? null : Math.max(0, pendingRemainingFromServer))
  const hasPendingPurchaseRequest = pendingRemainingSeconds != null && pendingRemainingSeconds > 0
  const isOwnListing = !!item && tokens?.userId === item.seller.id
  const canPurchase = item
    ? canPurchaseListing(item.statusCode, { hasPendingPurchaseRequest })
    : false
  const canEdit = item ? isOwnListing && canEditListing(item.statusCode) : false
  const canShare = item ? canShareListing(item.statusCode) : false
  const detailOverlay = item ? getListingDetailOverlay(item.statusCode, hasPendingPurchaseRequest) : null
  const showUnavailableBanner = item ? shouldShowUnavailableBanner(item.statusCode) : false
  const source = searchParams.get('from')
  const sourceConversationId = searchParams.get('conversationId')
  const shareDebugEnabled = searchParams.get('liffDebug') === '1'
  const backTarget = useMemo(() => {
    if (source === 'chat' && sourceConversationId) {
      return {
        to: `/messages/${sourceConversationId}`,
        label: '← 返回對話視窗',
      }
    }

    if (source === 'create' || source === 'edit') {
      return {
        to: '/my-listings',
        label: '← 返回我的商品',
      }
    }

    return {
      to: '/listings',
      label: '← 返回商品列表',
    }
  }, [source, sourceConversationId])

  useEffect(() => {
    if (!isAuthenticated || source !== 'create') {
      return
    }

    let disposed = false
    void accountApi
      .me()
      .then((profile) => {
        if (disposed) {
          return
        }
        const isLineBound = Boolean(profile.lineNotifyBound || profile.lineUserId)
        setLineBindPromptOpen(!isLineBound)
      })
      .catch(() => {
        if (!disposed) {
          setLineBindPromptOpen(false)
        }
      })

    return () => {
      disposed = true
    }
  }, [isAuthenticated, source])

  useEffect(() => {
    let disposed = false
    setIsLiffShareEnvReady(null)
    void getLiffShareDiagnostics()
      .then((diagnostics) => {
        if (disposed) {
          return
        }
        const envReady =
          diagnostics.liffReady && diagnostics.isInClient && diagnostics.shareTargetPickerAvailable
        setIsLiffShareEnvReady(envReady)
      })
      .catch(() => {
        if (!disposed) {
          setIsLiffShareEnvReady(false)
        }
      })

    return () => {
      disposed = true
    }
  }, [id])

  const handleChat = async () => {
    if (!item || conversationBusy) {
      return
    }
    if (!isAuthenticated) {
      navigate('/login')
      return
    }
    if (isOwnListing) {
      setError('這是你的商品，無法與自己建立對話')
      return
    }

    setConversationBusy(true)
    setError(null)
    try {
      const conversationId = await messagingApi.ensureConversation(item.id, item.seller.id)
      navigate(`/messages/${conversationId}`)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '建立對話失敗')
    } finally {
      setConversationBusy(false)
    }
  }

  const openPurchaseConfirm = () => {
    if (!item) {
      return
    }
    if (!canPurchase) {
      return
    }
    if (!isAuthenticated) {
      navigate('/login')
      return
    }
    if (isOwnListing) {
      setError('這是你的商品，無法購買自己的商品')
      return
    }
    setPurchaseConfirmOpen(true)
  }

  const handlePurchase = async () => {
    if (!item || purchaseBusy || !canPurchase) {
      return
    }

    setPurchaseBusy(true)
    setError(null)
    try {
      const request = await listingApi.createPurchaseRequest(item.id)
      setPurchaseConfirmOpen(false)
      navigate(`/messages/${request.conversationId}`)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '送出購買請求失敗')
    } finally {
      setPurchaseBusy(false)
    }
  }

  const handleStartLineBinding = async () => {
    if (lineBindBusy) {
      return
    }

    setLineBindBusy(true)
    setError(null)
    try {
      const binding = await accountApi.startLineBinding()
      saveLineBindingPending(binding.bindingToken, binding.botLink)
      const targetUrl = binding.liffUrl || binding.botLink
      if (!targetUrl) {
        setError('目前無法啟動 LINE 綁定，請稍後再試')
        return
      }
      window.location.assign(targetUrl)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '啟動 LINE 綁定失敗')
    } finally {
      setLineBindBusy(false)
    }
  }

  const getShareOptions = (targetItem: ListingDetail) => ({
    listingId: targetItem.id,
    listingTitle: targetItem.title,
    priceLabel: formatPrice(targetItem),
    categoryName: targetItem.categoryName,
    conditionName: targetItem.conditionName,
  })

  const executeShare = async (targetItem: ListingDetail) => {
    const result = await shareListingToLine(getShareOptions(targetItem))
    if (shareDebugEnabled) {
      setShareDebugResult(result)
    }
    if (result.usedFallbackUrlShare) {
      setError('目前已改用一般 LINE 連結分享；若要分享 Flex 訊息，請在 LINE App 內開啟。')
    }
  }

  const handleShareToLine = async () => {
    if (!item || !canShare) {
      return
    }

    setError(null)
    if (!shareDebugEnabled) {
      await executeShare(item)
      return
    }

    setShareDebugBusy(true)
    setShareDebugResult(null)
    const diagnostics = await getLiffShareDiagnostics()
    setShareDebugInfo(diagnostics)
    setShareDebugBusy(false)
    setShareDebugOpen(true)
  }

  const handleShareFlexOnly = async () => {
    if (!item || !canShare || flexShareBusy) {
      return
    }

    setFlexShareBusy(true)
    setError(null)
    try {
      const diagnostics = await getLiffShareDiagnostics()
      const envReady = diagnostics.liffReady && diagnostics.isInClient && diagnostics.shareTargetPickerAvailable
      setIsLiffShareEnvReady(envReady)
      if (!envReady) {
        setError('目前不是 LIFF 可分享 Flex 的環境，請改在 LINE App 內開啟。')
        return
      }

      const result = await shareListingToLineFlexOnly(getShareOptions(item))
      if (!result.sent && result.reason !== 'USER_CANCELLED_OR_CLOSED') {
        setError('Flex Message 發送失敗，請稍後再試。')
      }
    } catch {
      setError('Flex Message 發送失敗，請稍後再試。')
    } finally {
      setFlexShareBusy(false)
    }
  }

  const liffEnvText = isLiffShareEnvReady == null ? '檢查中' : isLiffShareEnvReady ? '是' : '否'

  const handleShareDebugConfirm = async () => {
    if (!item) {
      return
    }
    setShareDebugBusy(true)
    await executeShare(item)
    setShareDebugBusy(false)
  }

  const renderImagePanel = (extraClassName?: string) => (
    <div
      className={`relative overflow-hidden rounded-2xl border border-border bg-surface-2 lg:w-[44%] lg:shrink-0 ${
        extraClassName ?? ''
      }`}
    >
      {imageSlides.length > 0 ? (
        <ListingImageCarousel urls={imageSlides} title={item?.title ?? ''} />
      ) : (
        <div className="flex aspect-[16/10] items-center justify-center text-xl text-text-muted lg:aspect-[4/3]">無圖片</div>
      )}
      {detailOverlay === 'pending' ? (
        <motion.div className="absolute inset-0 z-20 flex flex-col items-center justify-center gap-2 bg-black/55 px-4 text-center text-white">
          <p className="text-xl font-semibold tracking-wide">交易處理中</p>
          <p className="text-3xl font-bold tabular-nums">{formatCountdown(pendingRemainingSeconds ?? 0)}</p>
        </motion.div>
      ) : detailOverlay === 'reserved' ? (
        <motion.div className="absolute inset-0 z-20 flex flex-col items-center justify-center gap-2 bg-black/55 px-4 text-center text-white">
          <p className="text-xl font-semibold tracking-wide">已保留</p>
        </motion.div>
      ) : null}
    </div>
  )

  return (
    <main className="mx-auto w-full max-w-7xl px-4 py-6 md:py-8">
      <Link to={backTarget.to} className="text-lg font-medium text-text-subtle hover:text-text-main">
        {backTarget.label}
      </Link>

      {loading ? <PageSkeleton className="mt-4 h-80" /> : null}
      {error ? <ErrorState description={error} /> : null}

      {!loading && !error && !item ? (
        <div className="mt-4">
          <EmptyState title="查無商品" description="這筆商品可能已下架或不存在。" />
        </div>
      ) : null}

      {item ? (
        <motion.section
          className="mt-4"
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.28, ease: [0.22, 1, 0.36, 1] }}
        >
          {showUnavailableBanner ? (
            <motion.div
              className="mb-4 rounded-2xl border border-[#D4A574] bg-[#FFF4E8] px-4 py-3 text-text-main"
              initial={{ opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ duration: 0.22 }}
            >
              <p className="text-lg font-semibold md:text-xl">{getUnavailableBannerMessage(item.statusCode)}</p>
            </motion.div>
          ) : null}
          <motion.div className="grid gap-4 lg:grid-cols-[minmax(0,0.76fr)_minmax(0,1.24fr)] lg:items-start">
            <motion.div
              initial={{ opacity: 0, x: -14 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ duration: 0.28, delay: 0.05, ease: [0.22, 1, 0.36, 1] }}
            >
            {renderImagePanel('mb-4 hidden lg:block lg:w-full')}
            <Card className="space-y-4 border-border !bg-[#F5EBDD] p-4 text-text-main md:p-5">
              <section className="space-y-3">
                <div className="flex items-start justify-between gap-3">
                  <div className="space-y-2">
                    <p className="text-3xl font-bold text-text-main md:text-4xl">{item.seller.displayName || '未提供'}</p>
                    <p className="text-base text-text-subtle md:text-lg">加入時間：{item.seller.memberDays} 天</p>
                  </div>
                  <Link
                    to={`/seller/${item.seller.id}`}
                    className="inline-flex shrink-0 items-center rounded-full border border-[#6E4F34] bg-[#EADBC8] px-3 py-1.5 text-sm font-semibold text-[#4A3423] transition hover:bg-[#E1CFB8]"
                  >
                    查看賣家頁面
                  </Link>
                </div>
                <div className="space-y-2">
                  <div className="flex flex-wrap gap-2 pt-1 text-sm font-semibold md:text-base">
                    <span className={`rounded-full px-2.5 py-1 ${item.seller.emailVerified ? 'bg-[#E3F6EC] text-[#2F7D4E]' : 'bg-surface text-text-muted'}`}>
                      {item.seller.emailVerified ? 'Email 已驗證' : 'Email 未驗證'}
                    </span>
                    <span className={`rounded-full px-2.5 py-1 ${item.seller.quickResponder ? 'bg-[#EFE9FF] text-[#5E5AB5]' : 'bg-surface text-text-muted'}`}>
                      {item.seller.quickResponder ? '快速回覆' : '一般回覆'}
                    </span>
                    <span className={`rounded-full px-2.5 py-1 ${item.seller.lineBound ? 'bg-[#E5F5E9] text-[#1F9D4D]' : 'bg-surface text-text-muted'}`}>
                      {item.seller.lineBound ? 'LINE 已綁定' : 'LINE 未綁定'}
                    </span>
                  </div>
                </div>
              </section>
            </Card>
            </motion.div>

            <motion.div
              initial={{ opacity: 0, x: 14 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ duration: 0.28, delay: 0.1, ease: [0.22, 1, 0.36, 1] }}
            >
            <Card className="space-y-4 border-border bg-surface p-4 md:p-5 lg:p-4">
              <section className="space-y-4">
                <div className="space-y-4">
                  {renderImagePanel('lg:hidden')}
                  <div className="space-y-4">
                    <div className="flex flex-wrap items-center gap-3">
                      <span className="rounded-full bg-surface-2 px-3 py-1 text-base font-semibold text-text-subtle md:text-lg">
                        {getListingStatusLabel(item.statusCode)}
                      </span>
                      <h3 className="text-3xl font-bold leading-tight text-text-main md:text-5xl lg:text-4xl">{item.title}</h3>
                    </div>
                    <div className="flex flex-wrap items-center gap-3">
                      {item.isFree ? (
                        <span className="inline-flex items-center rounded-full bg-[#2f7d4e] px-5 py-1.5 text-xl font-bold text-white md:text-2xl lg:text-xl">
                          免費
                        </span>
                      ) : (
                        <span className="text-3xl font-bold text-text-main md:text-4xl lg:text-3xl">{formatPrice(item)}</span>
                      )}
                    </div>
                    <div className="flex flex-wrap items-center gap-2 text-xl font-medium text-text-subtle md:text-2xl lg:text-xl">
                      <span>分類：{item.categoryName}</span>
                      <span>・</span>
                      <span>品況：{item.conditionName}</span>
                      <span>・</span>
                      <span>社宅：{item.residenceName}</span>
                    </div>
                    <p className="text-xl font-medium text-text-subtle md:text-2xl lg:text-xl">面交地點：{item.pickupLocationName}</p>
                    <p className="whitespace-pre-wrap text-lg leading-8 text-text-main md:text-2xl md:leading-9 lg:text-lg lg:leading-7">
                      {item.description || '賣家尚未提供描述。'}
                    </p>
                  </div>
                </div>
                <div className="space-y-3 pt-2">
                  <div className={`grid gap-3 ${isOwnListing || !canPurchase ? 'grid-cols-1' : 'grid-cols-2'}`}>
                  {isOwnListing ? (
                    canEdit ? (
                    <Link
                      to={`/listings/${item.id}/edit`}
                      className={getButtonClassName({
                        className: 'inline-flex min-h-[3.2rem] items-center justify-center text-xl font-semibold md:text-2xl',
                      })}
                    >
                      修改商品
                    </Link>
                    ) : (
                      <div className="space-y-3">
                        <p className="text-center text-lg font-medium text-text-subtle md:text-xl">
                          目前狀態：{getListingStatusLabel(item.statusCode)}
                        </p>
                        <Link
                          to="/my-listings"
                          className={getButtonClassName({
                            className:
                              'inline-flex min-h-[3.2rem] w-full items-center justify-center text-xl font-semibold md:text-2xl',
                          })}
                        >
                          返回我的商品
                        </Link>
                      </div>
                    )
                  ) : (
                    <>
                      <Button
                        type="button"
                        onClick={() => void handleChat()}
                        disabled={conversationBusy}
                        variant="secondary"
                        className="min-h-[3.2rem] text-xl font-semibold md:text-2xl"
                      >
                        {conversationBusy ? '連線中...' : '聊一下'}
                      </Button>
                      {canPurchase ? (
                        <Button
                          type="button"
                          onClick={openPurchaseConfirm}
                          disabled={purchaseBusy}
                          className="min-h-[3.2rem] text-xl font-semibold md:text-2xl"
                        >
                          {purchaseBusy ? '處理中...' : '購買'}
                        </Button>
                      ) : null}
                    </>
                  )}
                  </div>
                  <Button
                    type="button"
                    onClick={() => void handleShareToLine()}
                    disabled={!canShare}
                    variant="secondary"
                    className="min-h-[3.2rem] w-full text-xl font-semibold md:text-2xl"
                  >
                    分享到 LINE
                  </Button>
                  <Button
                    type="button"
                    onClick={() => void handleShareFlexOnly()}
                    disabled={!canShare || flexShareBusy}
                    variant="secondary"
                    className="min-h-[3.2rem] w-full text-lg font-semibold md:text-xl"
                  >
                    {flexShareBusy ? '發送中...' : `發送 Flex Message（LIFF 環境：${liffEnvText}）`}
                  </Button>
                </div>
              </section>
            </Card>
            </motion.div>
          </motion.div>
        </motion.section>
      ) : null}
      <PurchaseConfirmModal
        open={purchaseConfirmOpen}
        listingTitle={item?.title ?? ''}
        busy={purchaseBusy}
        onClose={() => setPurchaseConfirmOpen(false)}
        onConfirm={() => void handlePurchase()}
      />
      <ConfirmModal
        open={lineBindPromptOpen}
        busy={lineBindBusy}
        title="不漏接交易最後提醒"
        message="綁定 LINE 後，商品交易等待同意進入最後 1 小時時會通知你。平時不會頻繁打擾。"
        cancelLabel="稍後再說"
        confirmLabel="前往綁定 LINE"
        busyLabel="前往中..."
        onClose={() => setLineBindPromptOpen(false)}
        onConfirm={() => void handleStartLineBinding()}
      />
      <AppModal
        open={shareDebugOpen}
        onClose={() => {
          if (!shareDebugBusy) {
            setShareDebugOpen(false)
          }
        }}
        closeLabel="關閉 LINE 分享診斷視窗"
      >
        <div className="space-y-2">
          <h2 className="text-2xl font-bold text-text-main">LINE 分享診斷</h2>
          <p className="text-sm text-text-subtle">僅在網址帶 `liffDebug=1` 時顯示。</p>
        </div>
        <div className="space-y-1 rounded-xl bg-surface-2 p-3 text-sm text-text-main">
          <p>liffReady: {String(shareDebugInfo?.liffReady ?? false)}</p>
          <p>isInClient: {String(shareDebugInfo?.isInClient ?? false)}</p>
          <p>shareTargetPickerAvailable: {String(shareDebugInfo?.shareTargetPickerAvailable ?? false)}</p>
          <p>errorCode: {shareDebugInfo?.errorCode ?? '-'}</p>
          <p>errorMessage: {shareDebugInfo?.errorMessage ?? '-'}</p>
          <p>usedFallbackUrlShare: {shareDebugResult ? String(shareDebugResult.usedFallbackUrlShare) : '-'}</p>
        </div>
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
          <Button
            type="button"
            variant="secondary"
            disabled={shareDebugBusy}
            onClick={() => setShareDebugOpen(false)}
            className="min-h-[2.9rem] font-semibold"
          >
            關閉
          </Button>
          <Button
            type="button"
            disabled={shareDebugBusy}
            onClick={() => void handleShareDebugConfirm()}
            className="min-h-[2.9rem] font-semibold"
          >
            {shareDebugBusy ? '分享中...' : '繼續分享'}
          </Button>
        </div>
      </AppModal>
    </main>
  )
}
