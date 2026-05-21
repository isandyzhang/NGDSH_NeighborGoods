import { useEffect, useState } from 'react'
import {
  canOfferLineFlexShare,
  LINE_FLEX_SHARE_LABEL,
  LINE_TEXT_SHARE_LABEL,
  shareListingAsLineText,
  startListingFlexShare,
  type ShareListingOptions,
} from '@/features/listings/utils/lineShare'
import { Button, type ButtonVariant } from '@/shared/ui/Button'

type ListingLineShareButtonProps = {
  options: ShareListingOptions | null
  returnTo: string
  disabled?: boolean
  fullWidth?: boolean
  variant?: ButtonVariant
  className?: string
  noticeClassName?: string
  onNotice?: (message: string | null) => void
}

export const ListingLineShareButton = ({
  options,
  returnTo,
  disabled = false,
  fullWidth = true,
  variant = 'secondary',
  className = 'min-h-[3.2rem] text-lg font-semibold md:text-2xl',
  noticeClassName = 'text-base text-text-subtle',
  onNotice,
}: ListingLineShareButtonProps) => {
  const [modeReady, setModeReady] = useState(false)
  const [flexMode, setFlexMode] = useState(false)
  const [busy, setBusy] = useState(false)
  const [localNotice, setLocalNotice] = useState<string | null>(null)

  useEffect(() => {
    let disposed = false
    setModeReady(false)

    void (async () => {
      const offerFlex = await canOfferLineFlexShare()
      if (!disposed) {
        setFlexMode(offerFlex)
        setModeReady(true)
      }
    })()

    return () => {
      disposed = true
    }
  }, [])

  const setNotice = (message: string | null) => {
    setLocalNotice(message)
    onNotice?.(message)
  }

  const handleClick = async () => {
    if (!options || busy || disabled || !modeReady) {
      return
    }

    setBusy(true)
    setNotice(null)
    try {
      if (flexMode) {
        const result = await startListingFlexShare(options, returnTo)
        if (result.started === false) {
          setNotice(
            result.reason === 'LIFF_ID_MISSING'
              ? 'Flex 分享尚未設定（LIFF ID）。'
              : 'Flex 卡片分享請在 LINE App 內使用。',
          )
        }
        return
      }

      shareListingAsLineText(options)
    } finally {
      setBusy(false)
    }
  }

  const label = !modeReady
    ? '載入分享選項…'
    : flexMode
      ? busy
        ? '準備 Flex 分享…'
        : LINE_FLEX_SHARE_LABEL
      : busy
        ? '分享中…'
        : LINE_TEXT_SHARE_LABEL

  const notice = localNotice

  return (
    <div className="space-y-1">
      <Button
        type="button"
        fullWidth={fullWidth}
        variant={flexMode && modeReady ? 'primary' : variant}
        className={className}
        disabled={disabled || !options || busy || !modeReady}
        onClick={() => void handleClick()}
      >
        {label}
      </Button>
      {notice ? <p className={noticeClassName}>{notice}</p> : null}
    </div>
  )
}
