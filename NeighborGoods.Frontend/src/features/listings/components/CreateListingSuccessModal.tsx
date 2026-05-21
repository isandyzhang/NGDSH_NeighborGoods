import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { detectLiffInClient, shareListingAsLineText, startListingFlexShare } from '@/features/listings/utils/lineShare'
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
  const [flexShareBusy, setFlexShareBusy] = useState(false)
  const [lineInApp, setLineInApp] = useState(false)
  const [shareNotice, setShareNotice] = useState<string | null>(null)

  useEffect(() => {
    void detectLiffInClient().then((value) => setLineInApp(value === true))
  }, [])

  const shareOptions = () =>
    listing
      ? {
          listingId: listing.id,
          listingTitle: listing.title,
          priceLabel: formatSharePrice(listing),
          categoryName: listing.categoryName,
          conditionName: listing.conditionName,
        }
      : null

  const handleShareToLine = () => {
    const options = shareOptions()
    if (!options || shareBusy) {
      return
    }

    setShareBusy(true)
    setShareNotice(null)
    try {
      shareListingAsLineText(options)
    } finally {
      setShareBusy(false)
    }
  }

  const handleFlexShareToLine = async () => {
    const options = shareOptions()
    if (!options || flexShareBusy) {
      return
    }

    setFlexShareBusy(true)
    setShareNotice(null)
    try {
      const result = await startListingFlexShare(options, `/listings/${options.listingId}`)
      if (result.started === false) {
        setShareNotice(
          result.reason === 'LIFF_ID_MISSING'
            ? 'Flex 分享尚未設定（LIFF ID）。'
            : 'Flex 卡片分享請在 LINE App 內使用。',
        )
      }
    } finally {
      setFlexShareBusy(false)
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
          <div className="space-y-1">
            <Button
              type="button"
              fullWidth
              className="min-h-[3.2rem] text-lg font-semibold"
              disabled={!listing || shareBusy}
              onClick={handleShareToLine}
            >
              {shareBusy ? '分享中...' : '分享到 LINE'}
            </Button>
            {lineInApp ? (
              <button
                type="button"
                disabled={!listing || flexShareBusy}
                onClick={() => void handleFlexShareToLine()}
                className="w-full text-xs text-text-subtle underline-offset-2 hover:text-text-main hover:underline disabled:opacity-50"
              >
                {flexShareBusy ? '準備 Flex 分享…' : '以 Flex 卡片分享（選人）'}
              </button>
            ) : null}
          </div>
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
