import { useCallback, useEffect, useState } from 'react'
import { adminApi, type AdminMemberList } from '@/features/admin/api/adminApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

const inputClassName =
  'w-full rounded-xl border border-border bg-surface px-3 py-2 text-text-main outline-none transition placeholder:text-text-muted focus:border-brand'

const mask = (value: string | null) => {
  if (!value) return '-'
  if (value.length <= 4) return value
  return `***${value.slice(-4)}`
}
const formatDateTime = (value: string | null) => (value ? new Date(value).toLocaleString('zh-TW') : '-')

export const AdminMembersPage = () => {
  const [keyword, setKeyword] = useState('')
  const [page, setPage] = useState(1)
  const [data, setData] = useState<AdminMemberList | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const result = await adminApi.listMembers({ q: keyword.trim() || undefined, page, pageSize: 20 })
      setData(result)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '讀取會員列表失敗')
    } finally {
      setLoading(false)
    }
  }, [keyword, page])

  useEffect(() => {
    void load()
  }, [load])

  if (loading && !data) return <PageSkeleton className="h-96" />
  if (error && !data) return <ErrorState description={error} onRetry={() => void load()} />

  const totalPages = Math.max(data?.totalPages ?? 1, 1)
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold text-text-main">會員管理</h1>
      <Card className="flex gap-2">
        <input className={inputClassName} value={keyword} onChange={(e) => setKeyword(e.target.value)} placeholder="搜尋名稱 / Email / 帳號" />
        <Button type="button" variant="secondary" onClick={() => { setPage(1); void load() }}>
          查詢
        </Button>
      </Card>
      {error ? <p className="text-sm text-rose-600">{error}</p> : null}
      <div className="overflow-x-auto">
        <table className="min-w-full border-collapse text-left text-sm">
          <thead className="bg-surface-2">
            <tr>
              {['名稱', '帳號', 'Email', 'Email驗證', 'LINE', 'LINE聯絡', '角色', '建立', '最後登入', 'LINE授權', 'LINE偏好', '置頂點數', '快回', '電話', '鎖定', '有密碼'].map((h) => (
                <th key={h} className="border border-border px-2 py-2 whitespace-nowrap">{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data?.items.map((m) => (
              <tr key={m.id}>
                <td className="border border-border px-2 py-2 whitespace-nowrap">{m.displayName}</td>
                <td className="border border-border px-2 py-2 whitespace-nowrap">{m.userName ?? '-'}</td>
                <td className="border border-border px-2 py-2 whitespace-nowrap">{m.email ?? '-'}</td>
                <td className="border border-border px-2 py-2">{m.emailConfirmed ? 'Y' : 'N'}</td>
                <td className="border border-border px-2 py-2 whitespace-nowrap">{mask(m.lineUserId)}</td>
                <td className="border border-border px-2 py-2 whitespace-nowrap">{mask(m.lineContactId)}</td>
                <td className="border border-border px-2 py-2">{m.role}</td>
                <td className="border border-border px-2 py-2 whitespace-nowrap">{formatDateTime(m.createdAt)}</td>
                <td className="border border-border px-2 py-2 whitespace-nowrap">{formatDateTime(m.lastLoginAt)}</td>
                <td className="border border-border px-2 py-2 whitespace-nowrap">{formatDateTime(m.lineMessagingApiAuthorizedAt)}</td>
                <td className="border border-border px-2 py-2">{m.lineNotificationPreference}</td>
                <td className="border border-border px-2 py-2">{m.topPinCredits}</td>
                <td className="border border-border px-2 py-2">{m.isQuickResponder ? 'Y' : 'N'}</td>
                <td className="border border-border px-2 py-2 whitespace-nowrap">{m.phoneNumber ?? '-'}</td>
                <td className="border border-border px-2 py-2">{m.lockoutEnabled ? 'Y' : 'N'}</td>
                <td className="border border-border px-2 py-2">{m.hasPassword ? 'Y' : 'N'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
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
    </div>
  )
}
