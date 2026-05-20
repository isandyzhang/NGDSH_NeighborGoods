import { useEffect, useMemo, useState } from 'react'
import { useLocation } from 'react-router-dom'
import { getSeverityEmoji } from '@/features/admin/constants/announcementOptions'
import { announcementApi, type ActiveAnnouncement } from '@/features/announcements/api/announcementApi'

export const SiteAnnouncementBanner = () => {
  const { pathname } = useLocation()
  const [items, setItems] = useState<ActiveAnnouncement[]>([])

  const scopeQuery = useMemo(() => {
    if (pathname === '/listings' || pathname === '/listing') {
      return 1
    }

    return 0
  }, [pathname])

  useEffect(() => {
    let disposed = false

    void announcementApi
      .getActive(scopeQuery)
      .then((rows) => {
        if (!disposed) {
          setItems(rows)
        }
      })
      .catch(() => {
        if (!disposed) {
          setItems([])
        }
      })

    return () => {
      disposed = true
    }
  }, [scopeQuery, pathname])

  if (items.length === 0) {
    return null
  }

  return (
    <div className="font-sans" role="status" aria-live="polite">
      {items.map((item) => {
        const emoji = getSeverityEmoji(item.severity)
        const linkLabel = item.linkLabel?.trim() || '\u8a73\u60c5'

        return (
          <div
            key={item.id}
            className="bg-black/60 px-4 py-2 text-center text-sm text-white"
          >
            <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-center gap-x-3 gap-y-1">
              <p className="font-medium">
                <span className="mr-1.5" aria-hidden="true">
                  {emoji}
                </span>
                {item.message}
              </p>
              {item.linkUrl ? (
                <a
                  href={item.linkUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="shrink-0 text-white underline underline-offset-2 opacity-90 hover:opacity-100"
                >
                  {linkLabel}
                </a>
              ) : null}
            </div>
          </div>
        )
      })}
    </div>
  )
}
