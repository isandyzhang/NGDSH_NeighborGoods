import { AdminCategoriesCard } from '@/features/admin/components/housing/AdminCategoriesCard'
import { AdminPickupLocationsCard } from '@/features/admin/components/housing/AdminPickupLocationsCard'
import { AdminResidencesCard } from '@/features/admin/components/housing/AdminResidencesCard'

export const AdminHousingPage = () => {
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold text-text-main">社宅管理</h1>
      <p className="text-sm text-text-subtle">維護社宅、各社宅面交地點與商品分類，讓網站可給其他社宅使用。</p>
      <AdminResidencesCard />
      <AdminPickupLocationsCard />
      <AdminCategoriesCard />
    </div>
  )
}
