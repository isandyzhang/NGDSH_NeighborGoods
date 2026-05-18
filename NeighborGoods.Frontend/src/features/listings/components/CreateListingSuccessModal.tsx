import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { shareListingToLine } from '@/features/listings/utils/lineShare'
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

const formatSharePrice = (listing: CreatedListingSummary) =>
  listing.isFree ? '免費' : `NT$ ${listing.price.toLocaleString()}`

export const CreateListingSuccessModal = ({ open, listing, onClose }: CreateListingSuccessModalProps) => {
  const navigate = useNavigate()
  const [shareBusy, setShareBusy] = useState(false)
  const [shareNotice, setShareNotice] = useState<string | null>(null)

  const handleShareToLine = async () => {
    if (!listing || shareBusy) {
      return
    }

    setShareBusy(true)
    setShareNotice(null)
    try {
      const result = await shareListingToLine({
        listingId: listing.id,
        listingTitle: listing.title,
        priceLabel: formatSharePrice(listing),
        categoryName: listing.categoryName,
        conditionName: listing.conditionName,
      })
      if (result.usedFallbackUrlShare) {
        setShareNotice('目前已改用一般 LINE 連結分享；若要分享 Flex 訊息，請在 LINE App 內開啟。')
      }
    } finally {
      setShareBusy(false)
    }
  }

  const handleNavigate = (path: string) => {
    setShareNotice(null)
    onClose()
    navigate(path)
  }

  const handleClose = () => {
    setShareNotice(null)
    onClose()
  }

  return (
    <AppModal open={open} onClose={handleClose} closeLabel="關閉成功視窗" maxWidthClassName="max-w-md">
      <div className="space-y-5 text-center">
        <div className="space-y-3">
          <h2 className="text-2xl font-semibold text-text-main sm:text-3xl">產品新增成功</h2>
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
          <Button
            type="button"
            fullWidth
            className="min-h-[3.2rem] text-lg font-semibold"
            disabled={!listing || shareBusy}
            onClick={() => void handleShareToLine()}
          >
            {shareBusy ? '分享中...' : '分享到 LINE'}
          </Button>
          <Button
            type="button"
            variant="secondary"
            fullWidth
            className="min-h-[3.2rem] text-lg font-semibold"
            onClick={() => handleNavigate('/my-listings')}
          >
            我的商品
          </Button>
          <Button
            type="button"
            variant="secondary"
            fullWidth
            className="min-h-[3.2rem] text-lg font-semibold"
            onClick={() => handleNavigate('/listings')}
          >
            商品列表
          </Button>
        </div>

        {shareNotice ? <p className="text-base text-text-subtle">{shareNotice}</p> : null}
      </div>
    </AppModal>
  )
}
