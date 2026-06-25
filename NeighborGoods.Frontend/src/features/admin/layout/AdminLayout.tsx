import { Link, Outlet } from 'react-router-dom'
import { AdminTabNav } from '@/features/admin/layout/AdminTabNav'

export const AdminLayout = () => {
  return (
    <div className="pb-8">
      <div className="mx-auto flex max-w-6xl items-center justify-between px-4 pt-4">
        <Link to="/listings" className="text-sm text-text-subtle underline">
          ← 回到首頁
        </Link>
        <Link to="/admin/liff-debug" className="text-xs text-text-muted underline">
          LIFF 除錯
        </Link>
      </div>
      <AdminTabNav />
      <main className="mx-auto max-w-6xl px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}
