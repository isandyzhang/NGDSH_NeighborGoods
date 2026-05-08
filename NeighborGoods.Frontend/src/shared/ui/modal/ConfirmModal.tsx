import { Button } from '@/shared/ui/Button'
import { AppModal } from '@/shared/ui/modal/AppModal'

type ConfirmModalProps = {
  open: boolean
  busy: boolean
  title: string
  message: string
  cancelLabel: string
  confirmLabel: string
  busyLabel?: string
  cancelButtonClassName?: string
  confirmButtonClassName?: string
  onClose: () => void
  onConfirm: () => void
}

export const ConfirmModal = ({
  open,
  busy,
  title,
  message,
  cancelLabel,
  confirmLabel,
  busyLabel = '處理中...',
  cancelButtonClassName,
  confirmButtonClassName,
  onClose,
  onConfirm,
}: ConfirmModalProps) => {
  return (
    <AppModal open={open} onClose={onClose} closeLabel="關閉確認視窗">
      <div className="space-y-2">
        <h2 className="text-3xl font-bold text-text-main">{title}</h2>
        <p className="text-base leading-relaxed text-text-subtle">{message}</p>
      </div>
      <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
        <Button
          type="button"
          variant="secondary"
          onClick={onClose}
          disabled={busy}
          className={`min-h-[2.9rem] font-semibold ${cancelButtonClassName ?? ''}`}
        >
          {cancelLabel}
        </Button>
        <Button
          type="button"
          onClick={onConfirm}
          disabled={busy}
          className={`min-h-[2.9rem] font-semibold ${confirmButtonClassName ?? ''}`}
        >
          {busy ? busyLabel : confirmLabel}
        </Button>
      </div>
    </AppModal>
  )
}
