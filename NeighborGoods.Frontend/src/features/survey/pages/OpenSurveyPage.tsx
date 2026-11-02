import { useCallback, useEffect, useState } from 'react'
import { surveyApi, type OpenSurveyList, type OpenSurveyListItem } from '@/features/survey/api/surveyApi'
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
  })

const formatResidentType = (value: string) => (value === 'social' ? '社宅住戶' : '非社宅住戶')

const formatBuilding = (building: string | null, floor: number | null) => {
  if (!building) {
    return '-'
  }
  const name = building === 'south' ? '南棟' : building === 'north' ? '北棟' : building
  return floor ? `${name} ${floor}F` : name
}

const formatRating = (value: string) => {
  switch (value) {
    case 'satisfied':
      return '滿意'
    case 'fair':
      return '還好'
    case 'unsatisfied':
      return '不滿意'
    default:
      return value
  }
}

const formatChannel = (value: string) => {
  switch (value) {
    case 'friends':
      return '親友／管委'
    case 'bulletin':
      return '公佈欄／海報'
    case 'youth_online':
      return '青創線上'
    case 'other':
      return '其他'
    default:
      return value
  }
}

export const OpenSurveyPage = () => {
  const [data, setData] = useState<OpenSurveyList | null>(null)
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [selected, setSelected] = useState<OpenSurveyListItem | null>(null)

  const loadSurveys = useCallback(async (targetPage: number) => {
    setLoading(true)
    setError(null)
    try {
      const result = await surveyApi.listOpenSurveys({ page: targetPage, pageSize: 20 })
      setData(result)
      setPage(result.page)
      setTotalPages(Math.max(result.totalPages, 1))
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '讀取問卷失敗')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void loadSurveys(page)
  }, [loadSurveys, page])

  if (loading && !data) {
    return (
      <main className="mx-auto w-full max-w-6xl px-4 py-6 md:py-8">
        <PageSkeleton className="h-96" />
      </main>
    )
  }

  if (error && !data) {
    return (
      <main className="mx-auto w-full max-w-6xl px-4 py-6 md:py-8">
        <ErrorState description={error} onRetry={() => void loadSurveys(page)} />
      </main>
    )
  }

  return (
    <main className="mx-auto w-full max-w-6xl space-y-4 px-4 py-6 md:py-8">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="space-y-1">
          <h1 className="text-2xl font-semibold text-text-main">公開問卷回覆一覽</h1>
          <p className="text-sm text-text-subtle">姓名／手機已遮罩</p>
        </div>
        <Button type="button" variant="secondary" disabled={loading} onClick={() => void loadSurveys(page)}>
          重新整理
        </Button>
      </div>

      {data ? (
        <p className="text-sm text-text-subtle">
          贈品名額：剩餘 {data.remainingGiftSlots} / {data.giftLimit}（一瓶御茶園麥萃日式麥茶，用餐區自取）
        </p>
      ) : null}

      {error ? <p className="text-sm text-rose-600">{error}</p> : null}

      <div className="overflow-x-auto rounded-xl border border-border bg-surface">
        <table className="min-w-full text-left text-sm">
          <thead className="border-b border-border bg-surface-2 text-text-subtle">
            <tr>
              <th className="px-4 py-3 font-medium">時間</th>
              <th className="px-4 py-3 font-medium">身份</th>
              <th className="px-4 py-3 font-medium">姓名／手機</th>
              <th className="px-4 py-3 font-medium">住址</th>
              <th className="px-4 py-3 font-medium">管道</th>
              <th className="px-4 py-3 font-medium">贈品</th>
              <th className="px-4 py-3 font-medium">Feedback</th>
              <th className="px-4 py-3 font-medium" />
            </tr>
          </thead>
          <tbody>
            {data?.items.length ? (
              data.items.map((item) => (
                <tr key={item.id} className="border-b border-border last:border-b-0">
                  <td className="whitespace-nowrap px-4 py-3 text-text-main">{formatDateTime(item.createdAt)}</td>
                  <td className="whitespace-nowrap px-4 py-3 text-text-subtle">{formatResidentType(item.residentType)}</td>
                  <td className="whitespace-nowrap px-4 py-3 text-text-subtle">
                    {item.displayNameMasked || item.phoneMasked
                      ? `${item.displayNameMasked ?? '-'} / ${item.phoneMasked ?? '-'}`
                      : '-'}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-text-subtle">
                    {formatBuilding(item.building, item.floor)}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-text-subtle">{formatChannel(item.channel)}</td>
                  <td className="whitespace-nowrap px-4 py-3 text-xs text-text-muted">
                    {item.isGiftEligible ? '可領' : '-'}
                  </td>
                  <td className="max-w-xs truncate px-4 py-3 text-text-subtle">{item.feedback ?? '-'}</td>
                  <td className="whitespace-nowrap px-4 py-3 text-right">
                    <button type="button" className="underline" onClick={() => setSelected(item)}>
                      查看
                    </button>
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={8} className="px-4 py-8 text-center text-text-muted">
                  尚無問卷回覆
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

      <AppModal open={Boolean(selected)} onClose={() => setSelected(null)} maxWidthClassName="max-w-2xl">
        {selected ? (
          <div className="space-y-3 text-sm text-text-subtle">
            <p className="text-lg font-semibold text-text-main">問卷詳情</p>
            <p>時間：{formatDateTime(selected.createdAt)}</p>
            <p>身份：{formatResidentType(selected.residentType)}</p>
            <p>姓名：{selected.displayNameMasked ?? '-'}</p>
            <p>手機：{selected.phoneMasked ?? '-'}</p>
            <p>住址：{formatBuilding(selected.building, selected.floor)}</p>
            <p>管道：{formatChannel(selected.channel)}</p>
            <p>贈品：{selected.isGiftEligible ? '可領取（用餐區自取）' : '無'}</p>
            <div className="rounded-lg border border-border bg-surface-2 p-3">
              <p>介面與瀏覽：{formatRating(selected.ratingUi)}</p>
              <p>刊登與管理：{formatRating(selected.ratingListing)}</p>
              <p>互動與媒合：{formatRating(selected.ratingInteraction)}</p>
              <p>說明清楚度：{formatRating(selected.ratingGuide)}</p>
              <p>宣傳管道：{formatRating(selected.ratingPromo)}</p>
            </div>
            <p className="whitespace-pre-wrap">Feedback：{selected.feedback ?? '-'}</p>
          </div>
        ) : null}
      </AppModal>
    </main>
  )
}
