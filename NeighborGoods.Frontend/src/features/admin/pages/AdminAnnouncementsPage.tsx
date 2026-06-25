import { AdminAnnouncementsCard } from '@/features/admin/components/AdminAnnouncementsCard'

export const AdminAnnouncementsPage = () => {
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold text-text-main">跑馬燈管理</h1>
      <p className="text-sm text-text-subtle">維持既有功能，版面集中在本分頁操作公告新增、編輯與啟用狀態。</p>
      <AdminAnnouncementsCard />
    </div>
  )
}
