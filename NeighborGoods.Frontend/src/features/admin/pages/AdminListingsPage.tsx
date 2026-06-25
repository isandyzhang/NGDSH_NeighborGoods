import { useCallback, useEffect, useState } from 'react'
import { adminApi, type AdminListingManagement } from '@/features/admin/api/adminApi'
import { AdminListingEditModal } from '@/features/admin/components/listings/AdminListingEditModal'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

const inputClassName =
  'w-full rounded-xl border border-border bg-surface px-3 py-2 text-text-main outline-none transition placeholder:text-text-muted focus:border-brand'

const formatDateTime = (value: string) =>
  new Date(value).toLocaleString('zh-TW', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })

export const AdminListingsPage = () => {
  const [keyword, setKeyword] = useState('')
  const [status, setStatus] = useState<number | 'all'>('all')
  const [data, setData] = useState<AdminListingManagement | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [page, setPage] = useState(1)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [deletingId, setDeletingId] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const result = await adminApi.listListings({
        q: keyword.trim() || undefined,
        status: status === 'all' ? undefined : status,
        page,
        pageSize: 10,
      })
      setData(result)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '讀取商品列表失敗')
    } finally {
      setLoading(false)
    }
  }, [keyword, page, status])

  useEffect(() => {
    void load()
  }, [load])

  const handleHardDelete = async (id: string) => {
    if (!window.confirm('此操作不可復原，確認硬刪除？')) return
    setDeletingId(id)
    try {
      await adminApi.hardDeleteListing(id)
      await load()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '刪除失敗')
    } finally {
      setDeletingId(null)
    }
  }

  if (loading && !data) return <PageSkeleton className="h-96" />
  if (error && !data) return <ErrorState description={error} onRetry={() => void load()} />

  const totalPages = Math.max(data?.pagination.totalPages ?? 1, 1)
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold text-text-main">商品列表</h1>
      <Card className="grid gap-3 md:grid-cols-[1.5fr_1fr_auto]">
        <input
          value={keyword}
          onChange={(e) => setKeyword(e.target.value)}
          placeholder="搜尋商品標題或賣家"
          className={inputClassName}
        />
        <select className={inputClassName} value={status} onChange={(e) => setStatus(e.target.value === 'all' ? 'all' : Number(e.target.value))}>
          <option value="all">全部狀態</option>
          <option value={0}>0 上架中</option>
          <option value={1}>1 保留</option>
          <option value={2}>2 售出</option>
          <option value={3}>3 已贈與</option>
          <option value={4}>4 已下架</option>
          <option value={5}>5 已易物</option>
        </select>
        <Button type="button" variant="secondary" onClick={() => { setPage(1); void load() }}>
          查詢
        </Button>
      </Card>

      {error ? <p className="text-sm text-rose-600">{error}</p> : null}

      <Card className="space-y-2">
        {data?.items.length ? (
          data.items.map((item) => (
            <div key={item.id} className="flex items-center gap-3 rounded-xl border border-border bg-surface p-3">
              <button type="button" className="rounded border border-border px-2 py-1 text-sm" onClick={() => setEditingId(item.id)}>
                ✏️
              </button>
              <div className="min-w-0 flex-1">
                <p className="truncate font-semibold text-text-main">{item.title}</p>
                <p className="text-sm text-text-subtle">
                  {item.sellerDisplayName}・狀態 {item.status}・{formatDateTime(item.createdAt)}
                </p>
              </div>
              <Button
                type="button"
                variant="secondary"
                className="border-[#e9b4b4] bg-[#fbe2e2] text-[#b23a3a] hover:bg-[#f6d3d3]"
                onClick={() => void handleHardDelete(item.id)}
                disabled={deletingId === item.id}
              >
                硬刪除
              </Button>
            </div>
          ))
        ) : (
          <p className="text-sm text-text-subtle">暫無資料</p>
        )}
      </Card>

      <div className="flex items-center gap-3">
        <Button type="button" variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
          上一頁
        </Button>
        <span className="text-sm text-text-subtle">
          第 {page} / {totalPages} 頁
        </span>
        <Button type="button" variant="secondary" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
          下一頁
        </Button>
      </div>

      <AdminListingEditModal open={Boolean(editingId)} listingId={editingId} onClose={() => setEditingId(null)} onUpdated={() => void load()} />
    </div>
  )
}
