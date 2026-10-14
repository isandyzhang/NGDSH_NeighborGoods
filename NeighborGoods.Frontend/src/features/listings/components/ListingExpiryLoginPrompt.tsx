import { useEffect, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useAuth } from '@/features/auth/components/AuthProvider'
import { listingApi, type MyListingItem } from '@/features/listings/api/listingApi'
import { isAutoExpiredListing } from '@/features/listings/constants/listingStatus'
import {
  beginListingExpiryPrompt,
  finishListingExpiryPrompt,
  notifyListingExpiryUpdated,
  subscribeListingExpiryPrompt,
} from '@/features/listings/listingExpiryPromptSession'
import { formatListingPrice } from '@/features/listings/utils/listingFormat'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { AppModal } from '@/shared/ui/modal/AppModal'
import { ConfirmModal } from '@/shared/ui/modal/ConfirmModal'

const PAGE_SIZE = 100
const MAX_PAGES = 20
const PROMPT_OVERLAY_CLASS = 'z-[1100]'
const CONFIRM_OVERLAY_CLASS = 'z-[1200]'

const SOLD_CONFIRM_MESSAGE =
  '此商品有相關的對話記錄，建議您透過正常交易流程完成交易，這樣可以建立買賣雙方關聯並進行評價。您確定要直接標記為交易完成嗎？ 請更新商品狀態，避免買家撲空。'

const loadAutoExpiredListings = async (): Promise<MyListingItem[]> => {
  const expired: MyListingItem[] = []
  let page = 1
  let totalPages = 1

  while (page <= totalPages && page <= MAX_PAGES) {
    const payload = await listingApi.listMine(page, PAGE_SIZE)
    totalPages = Math.max(payload.pagination.totalPages, 1)
    expired.push(
      ...payload.items.filter((item) => isAutoExpiredListing(item.statusCode, item.autoExpiredAt)),
    )
    page += 1
  }

  expired.sort((left, right) => {
    const leftMs = Date.parse(left.listedAt)
    const rightMs = Date.parse(right.listedAt)
    if (Number.isNaN(leftMs) && Number.isNaN(rightMs)) {
      return 0
    }
    if (Number.isNaN(leftMs)) {
      return 1
    }
    if (Number.isNaN(rightMs)) {
      return -1
    }
    return leftMs - rightMs
  })

  return expired
}

const ListingExpiryLoginPromptSession = ({ sessionTick }: { sessionTick: number }) => {
  const queryClient = useQueryClient()
  const [items, setItems] = useState<MyListingItem[]>([])
  const [index, setIndex] = useState(0)
  const [renewBusy, setRenewBusy] = useState(false)
  const [soldBusy, setSoldBusy] = useState(false)
  const [soldConfirmOpen, setSoldConfirmOpen] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    const pending = beginListingExpiryPrompt(loadAutoExpiredListings)
    if (!pending) {
      return
    }

    pending
      .then((nextItems) => {
        if (cancelled) {
          return
        }
        setItems(nextItems)
        setIndex(0)
        setError(null)
        setSoldConfirmOpen(false)
        if (nextItems.length === 0) {
          finishListingExpiryPrompt()
        }
      })
      .catch(() => {
        if (cancelled) {
          return
        }
        finishListingExpiryPrompt()
      })

    return () => {
      cancelled = true
    }
  }, [sessionTick])

  const current = items[index]
  const busy = renewBusy || soldBusy
  const open = Boolean(current)

  const dismiss = () => {
    if (busy) {
      return
    }
    finishListingExpiryPrompt()
    setItems([])
    setSoldConfirmOpen(false)
    setError(null)
  }

  const advance = () => {
    const nextIndex = index + 1
    setError(null)
    setSoldConfirmOpen(false)
    if (nextIndex >= items.length) {
      finishListingExpiryPrompt()
      setItems([])
      setIndex(0)
      return
    }
    setIndex(nextIndex)
  }

  const publishUpdate = () => {
    notifyListingExpiryUpdated()
    void queryClient.invalidateQueries({ queryKey: ['listings'] })
  }

  const handleRenew = async () => {
    if (!current) {
      return
    }
    setRenewBusy(true)
    setError(null)
    try {
      await listingApi.renew(current.id)
      publishUpdate()
      advance()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '延續刊登失敗')
    } finally {
      setRenewBusy(false)
    }
  }

  const handleMarkSold = async () => {
    if (!current) {
      return
    }
    setSoldBusy(true)
    setError(null)
    try {
      const result = await listingApi.markSoldFromExpiry(current.id)
      setSoldConfirmOpen(false)
      if (result.warning) {
        window.alert(result.warning)
      }
      publishUpdate()
      advance()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '標記已成交失敗')
    } finally {
      setSoldBusy(false)
    }
  }

  return (
    <>
      <AppModal
        open={open}
        onClose={dismiss}
        closeLabel="稍後處理"
        maxWidthClassName="max-w-md"
        overlayClassName={PROMPT_OVERLAY_CLASS}
      >
        {current ? (
          <div className="space-y-4">
            <div className="flex items-start justify-between gap-3">
              <p className="text-sm font-semibold text-text-subtle">
                第 {index + 1} / {items.length} 件
              </p>
              <button
                type="button"
                onClick={dismiss}
                disabled={busy}
                className="text-sm font-medium text-text-subtle underline-offset-2 hover:underline disabled:opacity-60"
              >
                稍後處理
              </button>
            </div>
            <div className="overflow-hidden rounded-xl bg-surface-2">
              {current.mainImageUrl ? (
                <img src={current.mainImageUrl} alt={current.title} className="aspect-[4/3] w-full object-cover" />
              ) : (
                <div className="flex aspect-[4/3] items-center justify-center text-sm text-text-muted">無圖片</div>
              )}
            </div>
            <div className="space-y-1">
              <h2 className="text-2xl font-semibold leading-tight text-text-main">{current.title}</h2>
              <p className="text-lg font-semibold text-text-subtle">{formatListingPrice(current)}</p>
              <p className="text-base font-semibold text-text-main">此商品已刊登滿 14 天，目前為非活躍狀態</p>
              <p className="text-sm text-text-subtle">請選擇繼續活躍以重新曝光，或若已售出請更新商品狀態。</p>
            </div>
            <div className="flex flex-col gap-3">
              <Button
                type="button"
                onClick={() => void handleRenew()}
                disabled={busy}
                className="min-h-[2.75rem] text-base font-semibold"
              >
                {renewBusy ? '處理中...' : '繼續活躍'}
              </Button>
              <Button
                type="button"
                variant="secondary"
                onClick={() => setSoldConfirmOpen(true)}
                disabled={busy}
                className="min-h-[2.75rem] text-base font-semibold"
              >
                已經售出
              </Button>
            </div>
            {error ? <p className="text-sm text-red-600">{error}</p> : null}
          </div>
        ) : null}
      </AppModal>
      <ConfirmModal
        open={soldConfirmOpen}
        busy={soldBusy}
        onClose={() => {
          if (!soldBusy) {
            setSoldConfirmOpen(false)
          }
        }}
        title="標記為已成交"
        message={SOLD_CONFIRM_MESSAGE}
        confirmLabel="確認已成交"
        cancelLabel="取消"
        overlayClassName={CONFIRM_OVERLAY_CLASS}
        onConfirm={() => void handleMarkSold()}
      />
    </>
  )
}

export const ListingExpiryLoginPrompt = () => {
  const { isAuthenticated } = useAuth()
  const [sessionTick, setSessionTick] = useState(0)

  useEffect(() => subscribeListingExpiryPrompt(() => setSessionTick((value) => value + 1)), [])

  if (!isAuthenticated) {
    return null
  }

  return <ListingExpiryLoginPromptSession sessionTick={sessionTick} />
}
