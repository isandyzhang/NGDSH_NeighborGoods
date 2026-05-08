import { memo } from 'react'
import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { Rocket } from 'lucide-react'
import { type ListingItem } from '@/features/listings/api/listingApi'
import { Button } from '@/shared/ui/Button'

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

const parseApiDateToMs = (value: string) => {
  const hasTimezone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value)
  const normalized = hasTimezone ? value : `${value}Z`
  const parsed = Date.parse(normalized)
  return Number.isNaN(parsed) ? null : parsed
}

type Props = {
  item: ListingItem
  countdownNowMs: number
  isOwnListing: boolean
  isLiked: boolean
  displayInterestCount: number
  favoriteBusy: boolean
  conversationBusy: boolean
  purchaseBusy: boolean
  lineNotifyIcon: string
  emailNotifyIcon: string
  quickResponderIcon: string
  onPrefetchListingDetail: (id: string) => void
  onToggleFavorite: (item: ListingItem) => void | Promise<void>
  onOpenTopPinFlow: (item: ListingItem) => void
  onStartConversation: (item: ListingItem) => void | Promise<void>
  onOpenPurchaseConfirm: (item: ListingItem) => void
}

export const ListingGridCard = memo(({
  item,
  countdownNowMs,
  isOwnListing,
  isLiked,
  displayInterestCount,
  favoriteBusy,
  conversationBusy,
  purchaseBusy,
  lineNotifyIcon,
  emailNotifyIcon,
  quickResponderIcon,
  onPrefetchListingDetail,
  onToggleFavorite,
  onOpenTopPinFlow,
  onStartConversation,
  onOpenPurchaseConfirm,
}: Props) => {
  const pendingExpireAt = item.pendingPurchaseRequestExpireAt
  const pendingRemainingFromServer = item.pendingPurchaseRequestRemainingSeconds
  const pendingExpireAtMs = pendingExpireAt === null ? null : parseApiDateToMs(pendingExpireAt)
  const pendingRemainingFromNow = pendingExpireAtMs == null ? null : Math.max(0, Math.floor((pendingExpireAtMs - countdownNowMs) / 1000))
  const pendingRemainingSeconds =
    pendingRemainingFromNow ?? (pendingRemainingFromServer == null ? null : Math.max(0, pendingRemainingFromServer))
  const hasPendingPurchaseRequest = pendingRemainingSeconds != null && pendingRemainingSeconds > 0
  const hasInProgressTrade = item.inProgress && !hasPendingPurchaseRequest
  const isReservedListing = item.statusCode === 1
  const hidePurchaseButton = !isOwnListing && (hasPendingPurchaseRequest || hasInProgressTrade || isReservedListing)

  return (
    <motion.div
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
                <span className="rounded-full bg-[#D64545] px-2.5 py-1 text-xs font-semibold text-white">置頂中</span>
              </div>
            ) : null}
            <div className="absolute right-2 top-2 z-10">
              <span className="rounded-full bg-black/70 px-3 py-1 text-sm font-semibold text-white">{item.categoryName}</span>
            </div>
            <Link
              to={`/listings/${item.id}?from=listings`}
              className="block h-full w-full"
              aria-label={`查看商品：${item.title}`}
              onMouseEnter={() => onPrefetchListingDetail(item.id)}
            >
              {item.mainImageUrl ? (
                <img
                  src={item.mainImageUrl}
                  alt={item.title}
                  className="h-full w-full object-cover"
                  loading="lazy"
                  decoding="async"
                />
              ) : (
                <div className="flex h-full items-center justify-center text-sm text-text-muted">無圖片</div>
              )}
            </Link>
            {hasPendingPurchaseRequest ? (
              <div className="absolute inset-0 z-20 flex flex-col items-center justify-center gap-1 bg-black/55 px-3 text-center text-white">
                <p className="text-sm font-semibold tracking-wide">交易處理中</p>
                <p className="text-xl font-bold tabular-nums">{formatCountdown(pendingRemainingSeconds)}</p>
              </div>
            ) : hasInProgressTrade ? (
              <div className="absolute inset-0 z-20 flex flex-col items-center justify-center gap-1 bg-black/55 px-3 text-center text-white">
                <p className="text-sm font-semibold tracking-wide">交易進行中</p>
                <p className="text-sm text-white/90">{item.inProgressStage === 5 ? '待買家確認收貨' : '已同意，等待完成交易'}</p>
              </div>
            ) : isReservedListing ? (
              <div className="absolute inset-0 z-20 flex flex-col items-center justify-center gap-1 bg-black/55 px-3 text-center text-white">
                <p className="text-sm font-semibold tracking-wide">已保留</p>
              </div>
            ) : null}
          </div>

          <div className="space-y-1 px-4 pb-4 pt-4">
            <p className="text-xs font-medium tracking-wide text-text-muted">{item.conditionName}</p>
            <p className="text-xs font-medium tracking-wide text-text-muted">社宅：{item.residenceName}</p>
            <Link
              to={`/listings/${item.id}?from=listings`}
              className="block truncate text-2xl font-semibold text-text-main underline-offset-2 hover:underline"
              onMouseEnter={() => onPrefetchListingDetail(item.id)}
            >
              {item.title}
            </Link>
            <div className="flex items-center justify-between">
              <span className={`text-lg font-semibold ${item.isFree ? 'text-[#3C8A65]' : 'text-text-subtle'}`}>{formatPrice(item)}</span>
              {!isOwnListing ? (
                <button
                  type="button"
                  onClick={() => {
                    void onToggleFavorite(item)
                  }}
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
                  onClick={() => onOpenTopPinFlow(item)}
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
                  <img src={lineNotifyIcon} alt="" className="h-5 w-5 object-contain" aria-hidden="true" />
                  <span className="sr-only">LINE通知已綁定</span>
                </span>
              ) : null}
              {item.seller.emailNotificationEnabled ? (
                <span className="inline-flex h-6 w-6 items-center justify-center" title="Email通知已開啟">
                  <img src={emailNotifyIcon} alt="" className="h-5 w-5 object-contain" aria-hidden="true" />
                  <span className="sr-only">Email通知已開啟</span>
                </span>
              ) : null}
              {item.seller.quickResponder ? (
                <span className="inline-flex h-6 w-6 items-center justify-center" title="快速回覆勳章已獲得">
                  <img src={quickResponderIcon} alt="" className="h-5 w-5 object-contain" aria-hidden="true" />
                  <span className="sr-only">快速回覆勳章已獲得</span>
                </span>
              ) : null}
            </div>
          </div>
        </div>
        <div className={`grid gap-2 ${isOwnListing || hidePurchaseButton ? 'grid-cols-1' : 'grid-cols-2'}`}>
          <Button
            type="button"
            onClick={() => {
              void onStartConversation(item)
            }}
            disabled={isOwnListing || conversationBusy}
            variant="secondary"
            className="rounded-lg px-2.5 py-1.5 text-lg font-semibold"
          >
            {isOwnListing ? '自己的商品' : conversationBusy ? '連線中...' : '聊一下'}
          </Button>
          {!isOwnListing && !hidePurchaseButton ? (
            <Button
              type="button"
              onClick={() => onOpenPurchaseConfirm(item)}
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
})

ListingGridCard.displayName = 'ListingGridCard'
