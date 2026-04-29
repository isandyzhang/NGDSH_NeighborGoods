import { AnimatePresence, motion } from 'framer-motion'
import { CircleAlert } from 'lucide-react'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'

type PurchaseConfirmModalProps = {
  open: boolean
  listingTitle: string
  busy: boolean
  onClose: () => void
  onConfirm: () => void
}

export const PurchaseConfirmModal = ({ open, listingTitle, busy, onClose, onConfirm }: PurchaseConfirmModalProps) => {
  return (
    <AnimatePresence>
      {open ? (
        <motion.div
          className="fixed inset-0 z-40 flex items-center justify-center bg-black/55 px-4"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          role="dialog"
          aria-modal="true"
        >
          <button type="button" className="absolute inset-0" aria-label="關閉購買確認視窗" onClick={onClose} />
          <motion.div
            className="relative z-10 w-full max-w-xl"
            initial={{ opacity: 0, y: 18, scale: 0.98 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 18, scale: 0.98 }}
            transition={{ duration: 0.22, ease: [0.22, 1, 0.36, 1] }}
          >
            <Card className="space-y-4 rounded-2xl p-5">
              <div className="inline-flex h-10 w-10 items-center justify-center rounded-full bg-[#FCE9E9] text-[#D64545]">
                <CircleAlert className="h-5 w-5" aria-hidden="true" />
              </div>
              <div className="space-y-2">
                <h2 className="text-2xl font-bold text-text-main">確認送出購買請求？</h2>
                <p className="text-base leading-relaxed text-text-subtle">
                  你即將購買「{listingTitle || '此商品'}」。送出後商品會進入保留流程，等待賣家回覆；賣家有 12 小時可同意或拒絕。
                  確認要繼續嗎？
                </p>
              </div>
              <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                <Button type="button" variant="secondary" onClick={onClose} disabled={busy} className="min-h-[2.9rem] font-semibold">
                  先等等
                </Button>
                <Button type="button" onClick={onConfirm} disabled={busy} className="min-h-[2.9rem] font-semibold">
                  {busy ? '送出中...' : '確認購買'}
                </Button>
              </div>
            </Card>
          </motion.div>
        </motion.div>
      ) : null}
    </AnimatePresence>
  )
}
