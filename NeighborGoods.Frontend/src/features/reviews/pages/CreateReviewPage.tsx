import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { reviewApi, type ReviewStatus } from '@/features/reviews/api/reviewApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

export const CreateReviewPage = () => {
  const { requestId = '' } = useParams()
  const [status, setStatus] = useState<ReviewStatus | null>(null)
  const [rating, setRating] = useState(5)
  const [content, setContent] = useState('')
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [successText, setSuccessText] = useState<string | null>(null)

  useEffect(() => {
    if (!requestId) {
      return
    }

    let disposed = false
    setLoading(true)
    setError(null)

    void reviewApi
      .getStatus(requestId)
      .then((result) => {
        if (disposed) {
          return
        }
        setStatus(result)
      })
      .catch((err: unknown) => {
        if (disposed) {
          return
        }
        setError(err instanceof ApiClientError ? err.message : '讀取評價狀態失敗')
      })
      .finally(() => {
        if (!disposed) {
          setLoading(false)
        }
      })

    return () => {
      disposed = true
    }
  }, [requestId])

  const canSubmit = useMemo(() => Boolean(status?.canReview && !status.reviewed), [status?.canReview, status?.reviewed])

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!requestId || !canSubmit || submitting) {
      return
    }

    setSubmitting(true)
    setError(null)
    setSuccessText(null)
    try {
      const review = await reviewApi.create(requestId, { rating, content: content.trim() || null })
      setStatus((current) =>
        current
          ? {
              ...current,
              canReview: false,
              reviewed: true,
              reason: '你已完成評價',
              review,
            }
          : current,
      )
      setSuccessText('評價已送出，感謝你的回饋。')
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '送出評價失敗')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className="mx-auto w-full max-w-4xl px-4 py-6 md:py-8">
      <section className="mb-8 space-y-3 text-center">
        <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="text-4xl font-semibold leading-tight text-text-main sm:text-5xl md:text-6xl">
          交易<span className="marker-wipe">評價</span>
        </h1>
        <p className="text-lg text-text-subtle">完成交易後，留下你的體驗回饋，幫助社群建立信任。</p>
      </section>

      <div className="mb-4">
        <Link to="/messages" className="text-sm text-text-subtle hover:text-text-main">
          ← 返回訊息列表
        </Link>
      </div>

      {loading ? <PageSkeleton /> : null}
      {error ? <ErrorState description={error} /> : null}
      {successText ? <p className="mb-4 text-base text-[#2F7D4E]">{successText}</p> : null}

      {!loading && !status ? (
        <EmptyState title="找不到評價狀態" description="請確認交易請求是否存在，或稍後再試。" />
      ) : null}

      {!loading && status ? (
        <Card>
          {status.reviewed && status.review ? (
            <div className="space-y-3">
              <p className="text-lg font-semibold text-text-main">你已完成此交易評價</p>
              <p className="text-base text-text-subtle">評分：{status.review.rating} / 5</p>
              <p className="rounded-xl border border-border bg-surface-2 px-3 py-3 text-base text-text-main">
                {status.review.content || '（未填寫文字評價）'}
              </p>
              <p className="text-sm text-text-muted">
                提交時間：{new Date(status.review.createdAt).toLocaleString('zh-TW')}
              </p>
            </div>
          ) : status.canReview ? (
            <form className="space-y-4" onSubmit={handleSubmit}>
              <div className="space-y-2">
                <p className="text-base font-semibold text-text-main">評分</p>
                <div className="flex flex-wrap gap-2">
                  {[1, 2, 3, 4, 5].map((value) => (
                    <button
                      key={value}
                      type="button"
                      onClick={() => setRating(value)}
                      className={`min-h-[2.8rem] min-w-[3rem] rounded-xl border px-3 py-2 text-base font-semibold transition ${
                        rating === value
                          ? 'border-transparent bg-[#2F7D4E] text-white'
                          : 'border-border bg-surface text-text-main hover:bg-surface-2'
                      }`}
                    >
                      {value}
                    </button>
                  ))}
                </div>
              </div>
              <label className="flex flex-col gap-2 text-sm text-text-subtle">
                <span className="text-base font-semibold text-text-main">文字評價（選填）</span>
                <textarea
                  value={content}
                  onChange={(event) => setContent(event.target.value)}
                  className="min-h-32 w-full rounded-xl border border-border bg-surface px-3 py-2 text-base text-text-main outline-none transition focus:border-brand"
                  maxLength={500}
                  placeholder="例如：回覆快速、面交守時、商品狀況與描述一致..."
                />
              </label>
              <Button type="submit" fullWidth className="min-h-[3rem] text-base font-semibold" disabled={submitting}>
                {submitting ? '送出中...' : '送出評價'}
              </Button>
            </form>
          ) : (
            <EmptyState title="目前尚不可評價" description={status.reason ?? '此交易尚未符合評價條件。'} />
          )}
        </Card>
      ) : null}
    </main>
  )
}
