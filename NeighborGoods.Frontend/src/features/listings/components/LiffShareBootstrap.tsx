import { useEffect } from 'react'
import { ensureLiffReady } from '@/features/listings/utils/lineShare'

/** 在根路徑 `/` 預先 init LIFF，之後 SPA 導到商品頁時 shareTargetPicker 仍可用。 */
export const LiffShareBootstrap = () => {
  useEffect(() => {
    if (typeof window === 'undefined' || window.location.pathname !== '/') {
      return
    }

    void (async () => {
      try {
        const liffMod = await import('@line/liff')
        if (!liffMod.default.isInClient()) {
          return
        }
        await ensureLiffReady()
      } catch {
        // ignore — 分享時會再試
      }
    })()
  }, [])

  return null
}
