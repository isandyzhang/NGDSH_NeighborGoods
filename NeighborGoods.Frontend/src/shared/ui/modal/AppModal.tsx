import { AnimatePresence, motion } from 'framer-motion'
import type { ReactNode } from 'react'
import { Card } from '@/shared/ui/Card'

type AppModalProps = {
  open: boolean
  onClose: () => void
  closeLabel?: string
  maxWidthClassName?: string
  children: ReactNode
}

export const AppModal = ({
  open,
  onClose,
  closeLabel = '關閉視窗',
  maxWidthClassName = 'max-w-xl',
  children,
}: AppModalProps) => {
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
          <button type="button" className="absolute inset-0" aria-label={closeLabel} onClick={onClose} />
          <motion.div
            className={`relative z-10 w-full ${maxWidthClassName}`}
            initial={{ opacity: 0, y: 18, scale: 0.98 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 18, scale: 0.98 }}
            transition={{ duration: 0.22, ease: [0.22, 1, 0.36, 1] }}
          >
            <Card className="space-y-4 rounded-2xl p-5">{children}</Card>
          </motion.div>
        </motion.div>
      ) : null}
    </AnimatePresence>
  )
}
