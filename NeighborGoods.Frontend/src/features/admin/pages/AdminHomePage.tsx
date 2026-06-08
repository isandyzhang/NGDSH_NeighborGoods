import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { adminApi, type AdminDashboard, type AdminListingManagement } from '@/features/admin/api/adminApi'
import { AdminAnnouncementsCard } from '@/features/admin/components/AdminAnnouncementsCard'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

const formatDateTime = (value: string) =>
  new Date(value).toLocaleString('zh-TW', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })

const formatPrice = (price: number, isFree: boolean) => (isFree ? '免費' : `NT$ ${Math.round(price).toLocaleString('zh-TW')}`)

export const AdminHomePage = () => {
  const [dashboard, setDashboard] = useState<AdminDashboard | null>(null)
  const [listingData, setListingData] = useState<AdminListingManagement | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [keyword, setKeyword] = useState('')
  const [statusFilter, setStatusFilter] = useState<number | 'all'>('all')
  const [selectedIds, setSelectedIds] = useState<string[]>([])
  const [targetStatus, setTargetStatus] = useState(0)
  const [operationMessage, setOperationMessage] = useState<string | null>(null)
  const [operationLoading, setOperationLoading] = useState(false)

  const loadDashboard = useCallback(async (params?: { keyword?: string; statusFilter?: number | 'all' }) => {
    const appliedKeyword = (params?.keyword ?? '').trim()
    const appliedStatus = params?.statusFilter ?? 'all'
    const [dashboardData, listings] = await Promise.all([
      adminApi.getDashboard(),
      adminApi.listListings({
        q: appliedKeyword || undefined,
        status: appliedStatus === 'all' ? undefined : appliedStatus,
        page: 1,
        pageSize: 50,
      }),
    ])
    setDashboard(dashboardData)
    setListingData(listings)
    setSelectedIds((current) => current.filter((id) => listings.items.some((item) => item.id === id)))
  }, [])

  useEffect(() => {
    let disposed = false
    setLoading(true)
    setError(null)

    void loadDashboard({ keyword: '', statusFilter: 'all' })
      .catch((err: unknown) => {
        if (!disposed) {
          setError(err instanceof ApiClientError ? err.message : '讀取後台首頁失敗')
        }
      })
      .finally(() => {
        if (!disposed) {
          setLoading(false)
        }
      })

    return () => {
      disposed = true
    }
  }, [loadDashboard])

  const handleSearchAndFilter = () => {
    void loadDashboard({ keyword, statusFilter })
  }

  if (loading) {
    return (
      <div className="space-y-4">
        <PageSkeleton className="h-32" />
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <PageSkeleton className="h-28" />
          <PageSkeleton className="h-28" />
          <PageSkeleton className="h-28" />
          <PageSkeleton className="h-28" />
        </div>
        <PageSkeleton className="h-72" />
      </div>
    )
  }

  if (error || !dashboard) {
    return <ErrorState description={error ?? '讀取後台首頁失敗'} onRetry={() => void loadDashboard()} />
  }

  const kpiCards = [
    { label: '總商品數', value: dashboard.kpi.totalListings },
    { label: '上架中商品', value: dashboard.kpi.activeListings },
    { label: '已完成交易', value: dashboard.kpi.completedListings },
    { label: '待處理事項', value: dashboard.kpi.pendingTopSubmissions + dashboard.kpi.unreadAdminMessages },
  ]

  const handleBatchForceStatus = async () => {
    if (selectedIds.length === 0 || operationLoading) {
      return
    }

    setOperationLoading(true)
    setOperationMessage(null)
    try {
      const result = await adminApi.batchForceUpdateListingStatus(selectedIds, targetStatus)
      setOperationMessage(`已批次更新 ${result.updatedCount} 筆商品為狀態 ${result.status}`)
      await loadDashboard({ keyword, statusFilter })
      setSelectedIds([])
    } catch (err) {
      setOperationMessage(err instanceof ApiClientError ? err.message : '批次更新狀態失敗')
    } finally {
      setOperationLoading(false)
    }
  }

  const handleHardDeleteSelected = async () => {
    if (selectedIds.length === 0 || operationLoading) {
      return
    }

    const ok = window.confirm(`即將硬刪除 ${selectedIds.length} 筆商品，此操作不可復原，確定執行？`)
    if (!ok) {
      return
    }

    setOperationLoading(true)
    setOperationMessage(null)
    try {
      await Promise.all(selectedIds.map((id) => adminApi.hardDeleteListing(id)))
      setOperationMessage(`已硬刪除 ${selectedIds.length} 筆商品`)
      await loadDashboard({ keyword, statusFilter })
      setSelectedIds([])
    } catch (err) {
      setOperationMessage(err instanceof ApiClientError ? err.message : '批次硬刪除失敗')
    } finally {
      setOperationLoading(false)
    }
  }

  return (
    <div className="space-y-6">
      <p>
        <Link to="/admin/conversations" className="text-sm text-text-subtle underline">
          查看聊天室列表 →
        </Link>
      </p>

      <AdminAnnouncementsCard />

      <Card className="border-[#f2d59a] bg-[#fff7e0]">
        <p className="text-sm text-text-subtle">系統提醒</p>
        <p className="mt-1 text-lg font-semibold text-text-main">
          尚有 {dashboard.kpi.pendingTopSubmissions} 筆置頂投稿待審、{dashboard.kpi.unreadAdminMessages} 則管理訊息未讀。
        </p>
      </Card>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {kpiCards.map((item) => (
          <Card key={item.label}>
            <p className="text-sm text-text-subtle">{item.label}</p>
            <p className="mt-2 text-4xl font-bold text-text-main">{item.value.toLocaleString('zh-TW')}</p>
          </Card>
        ))}
      </div>

      <Card className="flex flex-wrap gap-3">
        <Link to="/listings">
          <Button type="button" variant="secondary">
            回到商品列表
          </Button>
        </Link>
        <Link to="/admin/liff-debug">
          <Button type="button" variant="secondary">
            LIFF init 除錯
          </Button>
        </Link>
      </Card>

      <Card className="border-[#e9b4b4] bg-[#fff4f4]">
        <p className="text-sm font-semibold uppercase tracking-[0.1em] text-[#a94442]">管理員強制操作（高風險）</p>
        <div className="mt-3 grid gap-3 md:grid-cols-[1.2fr_1fr_1fr_auto_auto]">
          <input value={keyword} onChange={(event) => setKeyword(event.target.value)} placeholder="搜尋商品標題或賣家" className="w-full rounded-xl border border-border bg-surface px-3 py-2 text-text-main outline-none transition placeholder:text-text-muted focus:border-brand" />
          <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value === 'all' ? 'all' : Number(event.target.value))} className="w-full rounded-xl border border-border bg-surface px-3 py-2 text-text-main outline-none transition focus:border-brand">
            <option value="all">全部狀態</option>
            <option value={0}>0 上架中</option>
            <option value={1}>1 保留</option>
            <option value={2}>2 售出</option>
            <option value={3}>3 已捐贈</option>
            <option value={4}>4 已下架</option>
            <option value={5}>5 已易物</option>
          </select>
          <select
            value={targetStatus}
            onChange={(event) => setTargetStatus(Number(event.target.value))}
            className="w-full rounded-xl border border-border bg-surface px-3 py-2 text-text-main outline-none transition focus:border-brand"
          >
            <option value={0}>0 上架中 Active</option>
            <option value={1}>1 保留 Reserved</option>
            <option value={2}>2 售出 Sold</option>
            <option value={3}>3 已捐贈 Donated</option>
            <option value={4}>4 已下架 Inactive</option>
            <option value={5}>5 已易物 GivenOrTraded</option>
          </select>
          <Button type="button" variant="secondary" onClick={handleSearchAndFilter} disabled={operationLoading}>
            搜尋/篩選
          </Button>
          <Button type="button" variant="secondary" onClick={() => void handleBatchForceStatus()} disabled={operationLoading || selectedIds.length === 0}>
            批次改狀態
          </Button>
          <Button
            type="button"
            variant="secondary"
            onClick={() => void handleHardDeleteSelected()}
            disabled={operationLoading || selectedIds.length === 0}
            className="border-[#e9b4b4] bg-[#fbe2e2] text-[#b23a3a] hover:bg-[#f6d3d3]"
          >
            批次硬刪除
          </Button>
        </div>
        <p className="mt-2 text-sm text-text-subtle">目前已勾選 {selectedIds.length} 筆商品</p>
        {operationMessage ? <p className="mt-2 text-sm text-text-subtle">{operationMessage}</p> : null}
      </Card>

      <Card>
        <h2 className="mb-3 text-xl font-semibold text-text-main">商品列表</h2>
        <div className="space-y-2">
          {listingData?.items.length ? (
            listingData.items.map((item) => (
              <label key={item.id} className="flex cursor-pointer items-center gap-3 rounded-xl border border-border bg-surface-2 p-3">
                <input
                  type="checkbox"
                  checked={selectedIds.includes(item.id)}
                  onChange={(event) =>
                    setSelectedIds((current) =>
                      event.target.checked ? [...current, item.id] : current.filter((id) => id !== item.id),
                    )
                  }
                />
                <div className="min-w-0 flex-1">
                  <p className="truncate font-semibold text-text-main">{item.title}</p>
                  <p className="text-sm text-text-subtle">
                    {item.sellerDisplayName}・{formatPrice(item.price, item.isFree)}・狀態 {item.status}・{formatDateTime(item.createdAt)}
                  </p>
                </div>
              </label>
            ))
          ) : (
            <p className="text-sm text-text-subtle">暫無資料</p>
          )}
        </div>
      </Card>
    </div>
  )
}
