import { AnimatePresence, motion } from 'framer-motion'
import type { RefObject } from 'react'
import { type ListingItem } from '@/features/listings/api/listingApi'
import { ListingGridCard } from '@/features/listings/components/ListingGridCard'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'

type Props = {
  items: ListingItem[]
  loading: boolean
  showLoadingSkeleton: boolean
  page: number
  totalPages: number
  hasMorePages: boolean
  countdownNowMs: number
  currentUserId?: string
  favoriteStateById: Record<string, { isFavorited: boolean; favoriteCount: number }>
  favoriteBusyIds: Set<string>
  conversationBusyIds: Set<string>
  purchaseBusyIds: Set<string>
  lineNotifyIcon: string
  emailNotifyIcon: string
  quickResponderIcon: string
  loadMoreSentinelRef: RefObject<HTMLDivElement | null>
  onPrefetchListingDetail: (id: string) => void
  onToggleFavorite: (item: ListingItem) => void | Promise<void>
  onOpenTopPinFlow: (item: ListingItem) => void
  onStartConversation: (item: ListingItem) => void | Promise<void>
  onOpenPurchaseConfirm: (item: ListingItem) => void
}

export const ListingHomeGrid = ({
  items,
  loading,
  showLoadingSkeleton,
  page,
  totalPages,
  hasMorePages,
  countdownNowMs,
  currentUserId,
  favoriteStateById,
  favoriteBusyIds,
  conversationBusyIds,
  purchaseBusyIds,
  lineNotifyIcon,
  emailNotifyIcon,
  quickResponderIcon,
  loadMoreSentinelRef,
  onPrefetchListingDetail,
  onToggleFavorite,
  onOpenTopPinFlow,
  onStartConversation,
  onOpenPurchaseConfirm,
}: Props) => (
  <>
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

    {items.length > 0 && (!loading || !showLoadingSkeleton) ? (
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
              const isLiked = favoriteState?.isFavorited ?? item.isFavorited
              const displayFavoriteCount = favoriteState?.favoriteCount ?? item.favoriteCount
              const favoriteBusy = favoriteBusyIds.has(item.id)
              const isOwnListing = currentUserId === item.seller.id
              const conversationBusy = conversationBusyIds.has(item.id)
              const purchaseBusy = purchaseBusyIds.has(item.id)

              return (
                <ListingGridCard
                  key={item.id}
                  item={item}
                  countdownNowMs={countdownNowMs}
                  isOwnListing={isOwnListing}
                  isLiked={isLiked}
                  favoriteCount={displayFavoriteCount}
                  favoriteBusy={favoriteBusy}
                  conversationBusy={conversationBusy}
                  purchaseBusy={purchaseBusy}
                  lineNotifyIcon={lineNotifyIcon}
                  emailNotifyIcon={emailNotifyIcon}
                  quickResponderIcon={quickResponderIcon}
                  onPrefetchListingDetail={onPrefetchListingDetail}
                  onToggleFavorite={onToggleFavorite}
                  onOpenTopPinFlow={onOpenTopPinFlow}
                  onStartConversation={onStartConversation}
                  onOpenPurchaseConfirm={onOpenPurchaseConfirm}
                />
              )
            })}
          </AnimatePresence>
        </motion.section>
        <footer className="mt-6 flex flex-col items-center gap-2">
          <div ref={loadMoreSentinelRef} className="h-1 w-full" aria-hidden="true" />
          <span className="text-sm text-text-subtle">
            已載入第 {page} / {totalPages} 頁
          </span>
          {loading && hasMorePages ? <span className="text-sm text-text-subtle">載入更多商品中...</span> : null}
          {!hasMorePages ? <span className="text-sm text-text-subtle">已經到底囉</span> : null}
        </footer>
      </>
    ) : null}
  </>
)
