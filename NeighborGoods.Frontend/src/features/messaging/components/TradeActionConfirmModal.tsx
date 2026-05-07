import { ConfirmModal } from '@/shared/ui/modal/ConfirmModal'

type TradeActionConfirmModalProps = {
  open: boolean
  busy: boolean
  title: string
  message: string
  finalConfirmLabel: string
  onClose: () => void
  onConfirm: () => void
}

export const TradeActionConfirmModal = ({
  open,
  busy,
  title,
  message,
  finalConfirmLabel,
  onClose,
  onConfirm,
}: TradeActionConfirmModalProps) => {
  return (
    <ConfirmModal
      open={open}
      busy={busy}
      title={title}
      message={message}
      cancelLabel="取消"
      confirmLabel={finalConfirmLabel}
      busyLabel="處理中..."
      onClose={onClose}
      onConfirm={onConfirm}
    />
  )
}
