import { useEffect, useState } from 'react'
import {
  canOfferLineFlexShare,
  LINE_FLEX_COMMUNITY_NOTICE,
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
  const [flexBusy, setFlexBusy] = useState(false)
  const [textBusy, setTextBusy] = useState(false)
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

  const actionDisabled = disabled || !options || !modeReady

  const handleFlexShare = async () => {
    if (!options || flexBusy || textBusy || actionDisabled) {
      return
    }

    setFlexBusy(true)
    setNotice(null)
    try {
      const result = await startListingFlexShare(options, returnTo)
      if (result.started === false) {
        setNotice(
          result.reason === 'LIFF_ID_MISSING'
            ? 'Flex 分享尚未設定（LIFF ID）。'
            : 'Flex 卡片分享請在 LINE App 內使用。',
        )
      }
    } finally {
      setFlexBusy(false)
    }
  }

  const handleTextShare = () => {
    if (!options || flexBusy || textBusy || actionDisabled) {
      return
    }

    setTextBusy(true)
    setNotice(null)
    try {
      shareListingAsLineText(options)
    } finally {
      setTextBusy(false)
    }
  }

  if (!modeReady) {
    return (
      <div className="space-y-2">
        <Button type="button" fullWidth={fullWidth} variant={variant} className={className} disabled>
          載入分享選項…
        </Button>
      </div>
    )
  }

  if (flexMode) {
    return (
      <div className="flex flex-col gap-2">
        <div className="space-y-1">
          <Button
            type="button"
            fullWidth={fullWidth}
            variant="primary"
            className={className}
            disabled={actionDisabled || flexBusy || textBusy}
            onClick={() => void handleFlexShare()}
          >
            {flexBusy ? '準備 Flex 分享…' : LINE_FLEX_SHARE_LABEL}
          </Button>
          <p className="text-center text-xs text-text-muted">{LINE_FLEX_COMMUNITY_NOTICE}</p>
        </div>
        <Button
          type="button"
          fullWidth={fullWidth}
          variant={variant}
          className={className}
          disabled={actionDisabled || flexBusy || textBusy}
          onClick={handleTextShare}
        >
          {textBusy ? '分享中…' : LINE_TEXT_SHARE_LABEL}
        </Button>
        {localNotice ? <p className={noticeClassName}>{localNotice}</p> : null}
      </div>
    )
  }

  return (
    <div className="space-y-1">
      <Button
        type="button"
        fullWidth={fullWidth}
        variant={variant}
        className={className}
        disabled={actionDisabled || textBusy}
        onClick={handleTextShare}
      >
        {textBusy ? '分享中…' : LINE_TEXT_SHARE_LABEL}
      </Button>
      {localNotice ? <p className={noticeClassName}>{localNotice}</p> : null}
    </div>
  )
}
