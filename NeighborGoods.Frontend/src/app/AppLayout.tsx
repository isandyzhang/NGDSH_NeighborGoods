import { Outlet } from 'react-router-dom'
import { SiteAnnouncementBanner } from '@/features/announcements/components/SiteAnnouncementBanner'
import { LiffShareBootstrap } from '@/features/listings/components/LiffShareBootstrap'
import { TopNav } from '@/shared/ui/TopNav'

export const AppLayout = () => {
  return (
    <div className="min-h-screen bg-bg text-text-main">
      <LiffShareBootstrap />
      <TopNav />
      <SiteAnnouncementBanner />
      <Outlet />
    </div>
  )
}
