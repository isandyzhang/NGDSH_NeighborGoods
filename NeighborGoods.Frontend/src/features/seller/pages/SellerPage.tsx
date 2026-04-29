import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { listingApi, type SellerListingItem, type SellerSummary } from '@/features/listings/api/listingApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

const statusText: Record<number, string> = {
  0: '上架中',
  1: '保留中',
  2: '已售出',
  3: '已贈與',
  4: '已下架',
  5: '已易物',
}

const formatPrice = (item: Pick<SellerListingItem, 'isFree' | 'price'>) => (item.isFree ? '免費' : `NT$ ${item.price.toLocaleString()}`)

export const SellerPage = () => {
  const { sellerId = '' } = useParams()
  const [seller, setSeller] = useState<SellerSummary | null>(null)
  const [items, setItems] = useState<SellerListingItem[]>([])
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!sellerId) {
      return
    }

    let disposed = false
    setLoading(true)
    setError(null)

    void listingApi
      .getSellerListings(sellerId, page, 20)
      .then((result) => {
        if (disposed) {
          return
        }
        setSeller(result.seller)
        setItems(result.items)
        setTotalPages(Math.max(result.pagination.totalPages, 1))
      })
      .catch((err: unknown) => {
        if (disposed) {
          return
        }
        setError(err instanceof ApiClientError ? err.message : '讀取賣家資訊失敗')
      })
      .finally(() => {
        if (!disposed) {
          setLoading(false)
        }
      })

    return () => {
      disposed = true
    }
  }, [page, sellerId])

  const titleText = useMemo(() => (seller ? `${seller.sellerDisplayName} 的賣場` : '賣場資訊'), [seller])

  return (
    <main className="mx-auto w-full max-w-6xl px-4 py-6 md:py-8">
      <section className="mb-8 space-y-3 text-center">
        <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="text-4xl font-semibold leading-tight text-text-main sm:text-5xl md:text-6xl">
          賣家<span className="marker-wipe">頁面</span>
        </h1>
        <p className="text-lg text-text-subtle">{titleText}</p>
      </section>

      {loading ? <PageSkeleton className="h-64" /> : null}
      {error ? <ErrorState description={error} /> : null}

      {!loading && !error && seller ? (
        <Card className="mb-4">
          <p className="text-2xl font-semibold text-text-main">{seller.sellerDisplayName}</p>
          <p className="mt-2 text-base text-text-subtle">
            全部刊登 {seller.totalListings} ｜ 目前上架 {seller.activeListings} ｜ 已完成交易 {seller.completedListings}
          </p>
          <p className="mt-2 text-sm text-text-muted">評價摘要：尚無公開評價資料。</p>
        </Card>
      ) : null}

      {!loading && !error && items.length === 0 ? (
        <EmptyState title="目前沒有可顯示的刊登" description="此賣家尚未公開可瀏覽的刊登內容。" />
      ) : null}

      {!loading && items.length > 0 ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {items.map((item) => (
            <Card key={item.id} className="space-y-3">
              <Link to={`/listings/${item.id}?from=seller`} className="block overflow-hidden rounded-xl bg-surface-2">
                {item.mainImageUrl ? (
                  <img src={item.mainImageUrl} alt={item.title} className="aspect-[4/3] w-full object-cover" />
                ) : (
                  <div className="flex aspect-[4/3] items-center justify-center text-sm text-text-muted">無圖片</div>
                )}
              </Link>
              <div className="space-y-2">
                <Link to={`/listings/${item.id}?from=seller`} className="line-clamp-2 text-xl font-semibold text-text-main hover:underline">
                  {item.title}
                </Link>
                <div className="flex flex-wrap gap-2 text-sm text-text-subtle">
                  <span className="rounded-full border border-[#D8C0A3] bg-[#F8EFE4] px-3 py-1">{item.categoryName}</span>
                  <span className="rounded-full border border-[#D8C0A3] bg-[#F8EFE4] px-3 py-1">{formatPrice(item)}</span>
                  <span className="rounded-full border border-[#D8C0A3] bg-[#F8EFE4] px-3 py-1">
                    {statusText[item.statusCode] ?? '未知狀態'}
                  </span>
                </div>
                <p className="text-sm text-text-muted">刊登於 {new Date(item.createdAt).toLocaleDateString('zh-TW')}</p>
              </div>
            </Card>
          ))}
        </div>
      ) : null}

      {!loading && totalPages > 1 ? (
        <div className="mt-6 flex items-center justify-center gap-3">
          <Button type="button" variant="secondary" disabled={page <= 1} onClick={() => setPage((current) => Math.max(1, current - 1))}>
            上一頁
          </Button>
          <span className="text-sm text-text-subtle">
            第 {page} / {totalPages} 頁
          </span>
          <Button
            type="button"
            variant="secondary"
            disabled={page >= totalPages}
            onClick={() => setPage((current) => Math.min(totalPages, current + 1))}
          >
            下一頁
          </Button>
        </div>
      ) : null}
    </main>
  )
}
