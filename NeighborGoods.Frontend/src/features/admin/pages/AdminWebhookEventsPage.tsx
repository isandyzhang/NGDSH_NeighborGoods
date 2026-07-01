import { useCallback, useEffect, useState } from 'react'
import {
  adminApi,
  type AdminAdoWebhookEventDetail,
  type AdminAdoWebhookEventList,
} from '@/features/admin/api/adminApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { AppModal } from '@/shared/ui/modal/AppModal'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

const toLocalDate = (value: string) => {
  const trimmed = value.trim()
  const normalized = /(?:Z|[+-]\d{2}:\d{2})$/i.test(trimmed) ? trimmed : `${trimmed}Z`
  return new Date(normalized)
}

const formatDateTime = (value: string) =>
  toLocalDate(value).toLocaleString('zh-TW', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  })

const formatJsonBody = (rawBody: string) => {
  try {
    return JSON.stringify(JSON.parse(rawBody), null, 2)
  } catch {
    return rawBody
  }
}

const formatEventType = (eventType: string | null) => eventType ?? 'unknown'

const formatResolveStatus = (status: string) => {
  switch (status) {
    case 'success':
      return '欄位名稱已解析'
    case 'partial':
      return '部分欄位仍為 GUID'
    case 'failed':
      return '欄位名稱解析失敗'
    case 'skipped':
      return '未解析欄位名稱'
    default:
      return '處理中'
  }
}

const formatLineNotifyStatus = (status: string) => {
  switch (status) {
    case 'sent':
      return '已送'
    case 'skipped':
      return '略過'
    case 'failed':
      return '失敗'
    default:
      return '處理中'
  }
}

export const AdminWebhookEventsPage = () => {
  const [data, setData] = useState<AdminAdoWebhookEventList | null>(null)
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [detail, setDetail] = useState<AdminAdoWebhookEventDetail | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)
  const [detailError, setDetailError] = useState<string | null>(null)
  const [detailTab, setDetailTab] = useState<'summary' | 'raw'>('summary')

  const loadEvents = useCallback(async (targetPage: number) => {
    setLoading(true)
    setError(null)
    try {
      const result = await adminApi.listAdoWebhookEvents({ page: targetPage, pageSize: 20 })
      setData(result)
      setPage(result.page)
      setTotalPages(Math.max(result.totalPages, 1))
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '讀取 webhook 紀錄失敗')
    } finally {
      setLoading(false)
    }
  }, [])

  const loadDetail = useCallback(async (eventId: string) => {
    setDetailLoading(true)
    setDetailError(null)
    try {
      const result = await adminApi.getAdoWebhookEvent(eventId)
      setDetail(result)
    } catch (err) {
      setDetailError(err instanceof ApiClientError ? err.message : '讀取 webhook 詳情失敗')
      setDetail(null)
    } finally {
      setDetailLoading(false)
    }
  }, [])

  useEffect(() => {
    void loadEvents(page)
  }, [loadEvents, page])

  const handleView = async (eventId: string) => {
    setSelectedId(eventId)
    setDetail(null)
    setDetailTab('summary')
    await loadDetail(eventId)
  }

  const handleCloseModal = () => {
    setSelectedId(null)
    setDetail(null)
    setDetailError(null)
    setDetailTab('summary')
  }

  if (loading && !data) {
    return <PageSkeleton className="h-96" />
  }

  if (error && !data) {
    return <ErrorState description={error} onRetry={() => void loadEvents(page)} />
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold text-text-main">Webhook 紀錄</h1>
        <Button type="button" variant="secondary" disabled={loading} onClick={() => void loadEvents(page)}>
          重新整理
        </Button>
      </div>

      {error ? <p className="text-sm text-rose-600">{error}</p> : null}

      <div className="overflow-x-auto rounded-xl border border-border bg-surface">
        <table className="min-w-full text-left text-sm">
          <thead className="border-b border-border bg-surface-2 text-text-subtle">
            <tr>
              <th className="px-4 py-3 font-medium">接收時間</th>
              <th className="px-4 py-3 font-medium">事件</th>
              <th className="px-4 py-3 font-medium">Work Item</th>
              <th className="px-4 py-3 font-medium">摘要</th>
              <th className="px-4 py-3 font-medium">欄位解析</th>
              <th className="px-4 py-3 font-medium">LINE</th>
              <th className="px-4 py-3 font-medium" />
            </tr>
          </thead>
          <tbody>
            {data?.items.length ? (
              data.items.map((item) => (
                <tr key={item.id} className="border-b border-border last:border-b-0">
                  <td className="whitespace-nowrap px-4 py-3 text-text-main">{formatDateTime(item.receivedAt)}</td>
                  <td className="whitespace-nowrap px-4 py-3">
                    <span className="rounded bg-surface-2 px-2 py-1 text-xs text-text-subtle">{formatEventType(item.eventType)}</span>
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-text-subtle">
                    {item.workItemId ? `#${item.workItemId}` : '-'}
                    {item.workItemTitle ? ` ${item.workItemTitle}` : ''}
                  </td>
                  <td className="max-w-md truncate px-4 py-3 text-text-subtle">{item.summaryPreview ?? item.rawBodyPreview}</td>
                  <td className="whitespace-nowrap px-4 py-3 text-xs text-text-muted">{formatResolveStatus(item.fieldResolveStatus)}</td>
                  <td className="whitespace-nowrap px-4 py-3 text-xs text-text-muted">{formatLineNotifyStatus(item.lineNotifyStatus)}</td>
                  <td className="whitespace-nowrap px-4 py-3 text-right">
                    <button type="button" className="underline" onClick={() => void handleView(item.id)}>
                      查看
                    </button>
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={7} className="px-4 py-8 text-center text-text-muted">
                  尚無 webhook 紀錄
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {totalPages > 1 ? (
        <div className="flex items-center gap-3">
          <Button type="button" variant="secondary" disabled={page <= 1 || loading} onClick={() => setPage((p) => p - 1)}>
            上一頁
          </Button>
          <span className="text-sm text-text-subtle">
            第 {page} / {totalPages} 頁
          </span>
          <Button
            type="button"
            variant="secondary"
            disabled={page >= totalPages || loading}
            onClick={() => setPage((p) => p + 1)}
          >
            下一頁
          </Button>
        </div>
      ) : null}

      <AppModal open={Boolean(selectedId)} onClose={handleCloseModal} maxWidthClassName="max-w-4xl">
        <div className="space-y-3">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="font-semibold text-text-main">Webhook 詳情</p>
            <div className="flex gap-2">
              <button
                type="button"
                className={`rounded px-3 py-1 text-sm ${detailTab === 'summary' ? 'bg-surface-2 font-semibold text-text-main' : 'text-text-subtle'}`}
                onClick={() => setDetailTab('summary')}
              >
                整理後
              </button>
              <button
                type="button"
                className={`rounded px-3 py-1 text-sm ${detailTab === 'raw' ? 'bg-surface-2 font-semibold text-text-main' : 'text-text-subtle'}`}
                onClick={() => setDetailTab('raw')}
              >
                原始 JSON
              </button>
            </div>
          </div>

          {detailLoading ? <p className="text-sm text-text-subtle">載入中...</p> : null}
          {detailError ? <p className="text-sm text-rose-600">{detailError}</p> : null}

          {detail ? (
            <>
              <div className="grid gap-2 text-sm text-text-subtle sm:grid-cols-2">
                <p>接收時間：{formatDateTime(detail.receivedAt)}</p>
                <p>事件：{formatEventType(detail.eventType)}</p>
                <p>Work Item：{detail.workItemId ? `#${detail.workItemId}` : '-'}</p>
                <p>欄位解析：{formatResolveStatus(detail.fieldResolveStatus)}</p>
                <p>LINE：{formatLineNotifyStatus(detail.lineNotifyStatus)}</p>
                {detail.projectName ? <p>專案：{detail.projectName}</p> : null}
                {detail.workItemUrl ? (
                  <p className="sm:col-span-2">
                    連結：
                    <a href={detail.workItemUrl} target="_blank" rel="noreferrer" className="underline">
                      {detail.workItemUrl}
                    </a>
                  </p>
                ) : null}
              </div>

              {detail.fieldResolveStatus !== 'success' ? (
                <p className="text-xs text-amber-700">
                  {detail.normalizeError
                    ? `欄位名稱解析錯誤：${detail.normalizeError}`
                    : '部分客製欄位可能仍顯示 GUID reference name。'}
                </p>
              ) : null}

              {detailTab === 'summary' ? (
                <pre className="max-h-[60vh] overflow-auto rounded-lg border border-border bg-surface-2 p-4 text-xs text-text-main whitespace-pre-wrap break-all">
                  {detail.normalizedSummary ?? '尚無整理後內容'}
                </pre>
              ) : (
                <pre className="max-h-[60vh] overflow-auto rounded-lg border border-border bg-surface-2 p-4 text-xs text-text-main whitespace-pre-wrap break-all">
                  {formatJsonBody(detail.rawBody)}
                </pre>
              )}
            </>
          ) : null}
        </div>
      </AppModal>
    </div>
  )
}
