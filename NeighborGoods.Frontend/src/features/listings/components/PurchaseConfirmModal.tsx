import { ConfirmModal } from '@/shared/ui/modal/ConfirmModal'

type PurchaseConfirmModalProps = {
  open: boolean
  listingTitle: string
  busy: boolean
  onClose: () => void
  onConfirm: () => void
}

export const PurchaseConfirmModal = ({ open, listingTitle, busy, onClose, onConfirm }: PurchaseConfirmModalProps) => {
  return (
    <ConfirmModal
      open={open}
      busy={busy}
      title="確認送出購買請求？"
      message={`你即將購買「${listingTitle || '此商品'}」。送出後商品會進入保留流程，等待賣家回覆；賣家有 12 小時可同意或拒絕。確認要繼續嗎？`}
      cancelLabel="先等等"
      confirmLabel="確認購買"
      busyLabel="送出中..."
      onClose={onClose}
      onConfirm={onConfirm}
    />
  )
}
