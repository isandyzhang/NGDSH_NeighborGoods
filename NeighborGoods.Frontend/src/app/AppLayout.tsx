import { useEffect } from 'react'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { SiteAnnouncementBanner } from '@/features/announcements/components/SiteAnnouncementBanner'
import { LiffShareBootstrap } from '@/features/listings/components/LiffShareBootstrap'
import { getListingShareRedirectFromBrokenPath } from '@/features/listings/listingShareSession'
import { TopNav } from '@/shared/ui/TopNav'

export const AppLayout = () => {
  const location = useLocation()
  const navigate = useNavigate()

  useEffect(() => {
    const fix = getListingShareRedirectFromBrokenPath(location.pathname, location.search)
    if (fix) {
      navigate({ pathname: fix.pathname, search: fix.search }, { replace: true })
    }
  }, [location.pathname, location.search, navigate])

  return (
    <div className="min-h-screen bg-bg text-text-main">
      <LiffShareBootstrap />
      <TopNav />
      <SiteAnnouncementBanner />
      <Outlet />
    </div>
  )
}
