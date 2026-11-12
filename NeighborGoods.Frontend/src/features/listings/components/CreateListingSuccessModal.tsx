import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { listingApi } from '@/features/listings/api/listingApi'
import { ListingLineShareButton } from '@/features/listings/components/ListingLineShareButton'
import type { ShareListingOptions } from '@/features/listings/utils/lineShare'
import { formatListingPrice } from '@/features/listings/utils/listingFormat'
import { ApiClientError } from '@/shared/types/api'
import { AppModal } from '@/shared/ui/modal/AppModal'
import { Button } from '@/shared/ui/Button'

export type CreatedListingSummary = {
  id: string
  title: string
  categoryName: string
  conditionName: string
  isFree: boolean
  price: number
}

type CreateListingSuccessModalProps = {
  open: boolean
  listing: CreatedListingSummary | null
  onClose: () => void
}

export const CreateListingSuccessModal = ({ open, listing, onClose }: CreateListingSuccessModalProps) => {
  const navigate = useNavigate()
  const [groupShareBusy, setGroupShareBusy] = useState(false)
  const [groupShareNotice, setGroupShareNotice] = useState<string | null>(null)

  const shareOptions = useMemo((): ShareListingOptions | null => {
    if (!listing) {
      return null
    }
    return {
      listingId: listing.id,
      listingTitle: listing.title,
      priceLabel: formatListingPrice(listing),
      categoryName: listing.categoryName,
      conditionName: listing.conditionName,
    }
  }, [listing])

  const returnTo = listing ? `/listings/${listing.id}` : '/listings'

  const handleNavigate = (path: string) => {
    onClose()
    navigate(path)
  }

  const handleClose = () => {
    setGroupShareNotice(null)
    onClose()
  }

  const handleShareToTransactionGroup = async () => {
    if (!listing || groupShareBusy) {
      return
    }

    setGroupShareBusy(true)
    setGroupShareNotice(null)
    try {
      await listingApi.shareToTransactionGroup(listing.id)
      setGroupShareNotice('已推播商品卡到交易群組。')
    } catch (err) {
      setGroupShareNotice(err instanceof ApiClientError ? err.message : '推播到交易群組失敗')
    } finally {
      setGroupShareBusy(false)
    }
  }

  return (
    <AppModal open={open} onClose={handleClose} closeLabel="關閉成功視窗" maxWidthClassName="max-w-md">
      <div className="space-y-5 text-center">
        <div className="space-y-3">
          <h2 className="text-2xl font-semibold text-text-main sm:text-3xl">產品新增成功 🎆</h2>
          {listing ? (
            <>
              <p className="text-lg text-text-subtle">
                感謝你的刊登！「{listing.title}」已建立，你可以分享到 LINE，或前往管理商品。
              </p>
              <p className="text-base leading-relaxed text-text-subtle">
                若已在其他地方完成交易，或商品已經售出，請至「我的商品」將狀態更新為「已售出」，謝謝。
              </p>
            </>
          ) : null}
        </div>

        <div className="flex flex-col gap-3">
          <ListingLineShareButton
            options={shareOptions}
            returnTo={returnTo}
            variant="primary"
            className="min-h-[3.2rem] text-xl font-semibold sm:text-2xl"
          />
          <div className="space-y-1">
            <Button
              type="button"
              variant="secondary"
              fullWidth
              className="min-h-[3.8rem] text-2xl font-semibold sm:text-3xl"
              disabled={!listing || groupShareBusy}
              onClick={() => void handleShareToTransactionGroup()}
            >
              <span className="inline-flex items-center justify-center gap-2">
                <span>{groupShareBusy ? '推播中…' : '分享到交易群組'}</span>
                {!groupShareBusy ? (
                  <span className="inline-flex items-center gap-1 rounded-md border border-border px-2 py-0.5 text-sm font-medium">
                    👍 推薦
                  </span>
                ) : null}
              </span>
            </Button>
            <p className="text-sm text-text-subtle">
              由官方帳號把商品卡推播到社宅交易群，讓更多厝邊一次看到。
            </p>
            {groupShareNotice ? <p className="text-sm text-text-subtle">{groupShareNotice}</p> : null}
          </div>
          <Button
            type="button"
            variant="secondary"
            fullWidth
            className="min-h-[3.2rem] text-xl font-semibold sm:text-2xl"
            onClick={() => handleNavigate('/my-listings')}
          >
            我的商品
          </Button>
          <Button
            type="button"
            variant="secondary"
            fullWidth
            className="min-h-[3.2rem] text-xl font-semibold sm:text-2xl"
            onClick={() => handleNavigate('/listings')}
          >
            商品列表
          </Button>
        </div>
      </div>
    </AppModal>
  )
}
