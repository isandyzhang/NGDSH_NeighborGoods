import { AnimatePresence, motion } from 'framer-motion'
import { X } from 'lucide-react'
import { useEffect, type ReactNode } from 'react'

type ListingImageLightboxProps = {
  open: boolean
  onClose: () => void
  children: ReactNode
}

export const ListingImageLightbox = ({ open, onClose, children }: ListingImageLightboxProps) => {
  useEffect(() => {
    if (!open) {
      return
    }

    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    window.addEventListener('keydown', handleKeyDown)

    return () => {
      document.body.style.overflow = previousOverflow
      window.removeEventListener('keydown', handleKeyDown)
    }
  }, [open, onClose])

  return (
    <AnimatePresence>
      {open ? (
        <motion.div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 px-4 py-12"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          role="dialog"
          aria-modal="true"
          aria-label="商品照片放大檢視"
        >
          <button
            type="button"
            className="absolute inset-0"
            aria-label="關閉放大檢視"
            onClick={onClose}
          />
          <button
            type="button"
            aria-label="關閉"
            onClick={onClose}
            className="absolute right-3 top-3 z-20 flex size-10 items-center justify-center rounded-full bg-white/15 text-white transition hover:bg-white/25 md:right-5 md:top-5"
          >
            <X className="size-6" aria-hidden />
          </button>
          <motion.div
            className="relative z-10 w-full max-w-6xl"
            initial={{ opacity: 0, scale: 0.98 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0, scale: 0.98 }}
            transition={{ duration: 0.22, ease: [0.22, 1, 0.36, 1] }}
            onClick={(event) => event.stopPropagation()}
          >
            {children}
          </motion.div>
        </motion.div>
      ) : null}
    </AnimatePresence>
  )
}
