import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { listingApi, type ListingDetail } from '@/features/listings/api/listingApi'
import { ApiClientError } from '@/shared/types/api'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

const formatPrice = (item: ListingDetail) => {
  if (item.isFree) {
    return '免費'
  }

  return `NT$ ${item.price.toLocaleString()}`
}

const formatCountdown = (seconds: number) => {
  const normalized = Math.max(0, Math.floor(seconds))
  const hours = Math.floor(normalized / 3600)
  const minutes = Math.floor((normalized % 3600) / 60)
  const remainingSeconds = normalized % 60
  return [hours, minutes, remainingSeconds].map((value) => value.toString().padStart(2, '0')).join(':')
}

export const ListingDetailPage = () => {
  const { id = '' } = useParams()
  const [item, setItem] = useState<ListingDetail | null>(null)
  const [countdownNowMs, setCountdownNowMs] = useState(() => Date.now())
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!id) {
      return
    }

    let disposed = false
    setLoading(true)
    setError(null)

    void listingApi
      .getById(id)
      .then((data) => {
        if (!disposed) {
          setItem(data)
        }
      })
      .catch((err: unknown) => {
        if (disposed) {
          return
        }

        const message = err instanceof ApiClientError ? err.message : '讀取商品詳情失敗'
        setError(message)
      })
      .finally(() => {
        if (!disposed) {
          setLoading(false)
        }
      })

    return () => {
      disposed = true
    }
  }, [id])

  useEffect(() => {
    if (!item?.pendingPurchaseRequestExpireAt) {
      return
    }

    const timer = window.setInterval(() => {
      setCountdownNowMs(Date.now())
    }, 1000)

    return () => {
      window.clearInterval(timer)
    }
  }, [item?.pendingPurchaseRequestExpireAt])

  const pendingRemainingSeconds = !item?.pendingPurchaseRequestExpireAt
    ? null
    : Math.max(0, Math.floor((new Date(item.pendingPurchaseRequestExpireAt).getTime() - countdownNowMs) / 1000))
  const hasPendingPurchaseRequest = pendingRemainingSeconds != null && pendingRemainingSeconds > 0

  return (
    <main className="mx-auto w-full max-w-6xl px-4 py-6 md:py-8">
      <section className="mb-8 space-y-3 text-center">
        <p className="text-base uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="text-5xl font-semibold leading-tight text-text-main sm:text-6xl md:text-7xl">
          商品<span className="marker-wipe">詳情</span>
        </h1>
      </section>
      <Link to="/listings" className="text-lg font-medium text-text-subtle hover:text-text-main">
        ← 返回商品列表
      </Link>

      {loading ? <PageSkeleton className="mt-4 h-80" /> : null}
      {error ? <ErrorState description={error} /> : null}

      {!loading && !error && !item ? (
        <div className="mt-4">
          <EmptyState title="查無商品" description="這筆商品可能已下架或不存在。" />
        </div>
      ) : null}

      {item ? (
        <section className="mt-4">
          <Card className="overflow-hidden p-0">
            <div className="relative aspect-[4/3] overflow-hidden bg-surface-2">
              {item.mainImageUrl ? (
                <img src={item.mainImageUrl} alt={item.title} className="h-full w-full object-cover" />
              ) : (
                <div className="flex h-full items-center justify-center text-xl text-text-muted">無圖片</div>
              )}
              {hasPendingPurchaseRequest ? (
                <div className="absolute inset-0 z-20 flex flex-col items-center justify-center gap-2 bg-black/55 px-4 text-center text-white">
                  <p className="text-xl font-semibold tracking-wide">交易處理中</p>
                  <p className="text-4xl font-bold tabular-nums">{formatCountdown(pendingRemainingSeconds)}</p>
                </div>
              ) : null}
            </div>

            <div className="space-y-4 p-6 md:p-7">
              <h2 className="text-4xl font-bold leading-tight text-text-main md:text-5xl">{item.title}</h2>
              <div className="flex flex-wrap items-center gap-3">
                {item.isFree ? (
                  <span className="inline-flex items-center rounded-full bg-[#2f7d4e] px-5 py-1.5 text-xl font-bold text-white md:text-2xl">
                    免費
                  </span>
                ) : (
                  <span className="text-3xl font-bold text-text-main md:text-4xl">{formatPrice(item)}</span>
                )}
              </div>
              <p className="text-xl font-medium text-text-subtle md:text-2xl">
                {item.categoryName}・{item.conditionName}・{item.residenceName}
              </p>
              <p className="text-xl font-medium text-text-subtle md:text-2xl">面交地點：{item.pickupLocationName}</p>
              <p className="whitespace-pre-wrap text-xl leading-9 text-text-main md:text-2xl">
                {item.description || '賣家尚未提供描述。'}
              </p>
            </div>
          </Card>
          <div className="mt-4 grid grid-cols-2 gap-3">
            <Link
              to="/listings/create"
              className="inline-flex min-h-[3.4rem] items-center justify-center rounded-xl border border-border bg-surface px-4 text-xl font-semibold text-text-main transition hover:bg-surface-2"
            >
              繼續刊登
            </Link>
            <Link
              to="/my-listings"
              className="inline-flex min-h-[3.4rem] items-center justify-center rounded-xl bg-brand px-4 text-xl font-semibold text-brand-foreground transition hover:bg-brand-strong"
            >
              我的商品
            </Link>
          </div>
        </section>
      ) : null}
    </main>
  )
}
