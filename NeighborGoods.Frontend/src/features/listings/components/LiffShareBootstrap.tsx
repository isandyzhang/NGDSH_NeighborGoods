import { useEffect } from 'react'
import { isListingShareEntry } from '@/app/liffRoute'
import { isListingShareUrlReady } from '@/features/listings/listingShareSession'
import { ensureLiffReady } from '@/features/listings/utils/lineShare'

/** 在根路徑 `/` 預先 init LIFF，之後 SPA 導到商品頁時 shareTargetPicker 仍可用。 */
export const LiffShareBootstrap = () => {
  useEffect(() => {
    if (typeof window === 'undefined' || window.location.pathname !== '/') {
      return
    }

    const { pathname, search } = window.location
    // 分享頁自行 init；避免與 shareListingToLineFlexOnly 搶同一個 liff.init()
    if (isListingShareEntry(pathname, search) || !isListingShareUrlReady(pathname, search)) {
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
