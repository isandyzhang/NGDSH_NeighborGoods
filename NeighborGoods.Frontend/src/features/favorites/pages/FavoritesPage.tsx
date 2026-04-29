import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { listingApi, type FavoriteListingItem } from '@/features/listings/api/listingApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

const formatPrice = (item: Pick<FavoriteListingItem, 'isFree' | 'price'>) => (item.isFree ? '免費' : `NT$ ${item.price.toLocaleString()}`)

export const FavoritesPage = () => {
  const [items, setItems] = useState<FavoriteListingItem[]>([])
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [removingId, setRemovingId] = useState<string | null>(null)

  useEffect(() => {
    let disposed = false
    setLoading(true)
    setError(null)

    void listingApi
      .listFavorites(page, 20)
      .then((result) => {
        if (disposed) {
          return
        }
        setItems(result.items)
        setTotalPages(Math.max(result.pagination.totalPages, 1))
      })
      .catch((err: unknown) => {
        if (disposed) {
          return
        }
        setError(err instanceof ApiClientError ? err.message : '讀取收藏列表失敗')
      })
      .finally(() => {
        if (!disposed) {
          setLoading(false)
        }
      })

    return () => {
      disposed = true
    }
  }, [page])

  const handleUnfavorite = async (listingId: string) => {
    if (removingId) {
      return
    }

    setRemovingId(listingId)
    setError(null)
    try {
      await listingApi.unfavorite(listingId)
      setItems((current) => current.filter((item) => item.listingId !== listingId))
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '取消收藏失敗')
    } finally {
      setRemovingId(null)
    }
  }

  return (
    <main className="mx-auto w-full max-w-6xl px-4 py-6 md:py-8">
      <section className="mb-8 space-y-3 text-center">
        <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="text-4xl font-semibold leading-tight text-text-main sm:text-5xl md:text-6xl">
          我的<span className="marker-wipe">收藏</span>
        </h1>
        <p className="text-lg text-text-subtle">快速查看你已收藏的商品，並可直接取消收藏。</p>
      </section>

      {loading ? <PageSkeleton className="h-64" /> : null}
      {error ? <ErrorState description={error} /> : null}

      {!loading && !error && items.length === 0 ? (
        <EmptyState title="目前沒有收藏商品" description="到商品列表逛逛，按下收藏後會顯示在這裡。" />
      ) : null}

      {!loading && items.length > 0 ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {items.map((item) => (
            <Card key={item.listingId} className="space-y-3">
              <Link to={`/listings/${item.listingId}?from=favorites`} className="block overflow-hidden rounded-xl bg-surface-2">
                {item.mainImageUrl ? (
                  <img src={item.mainImageUrl} alt={item.title} className="aspect-[4/3] w-full object-cover" />
                ) : (
                  <div className="flex aspect-[4/3] items-center justify-center text-sm text-text-muted">無圖片</div>
                )}
              </Link>
              <div className="space-y-2">
                <Link to={`/listings/${item.listingId}?from=favorites`} className="line-clamp-2 text-xl font-semibold text-text-main hover:underline">
                  {item.title}
                </Link>
                <div className="flex flex-wrap gap-2 text-sm text-text-subtle">
                  <span className="rounded-full border border-[#D8C0A3] bg-[#F8EFE4] px-3 py-1">{item.categoryName}</span>
                  <span className="rounded-full border border-[#D8C0A3] bg-[#F8EFE4] px-3 py-1">{formatPrice(item)}</span>
                </div>
                <p className="text-sm text-text-muted">
                  收藏於 {new Date(item.favoritedAt).toLocaleDateString('zh-TW')}
                </p>
              </div>
              <Button
                type="button"
                variant="secondary"
                className="min-h-[3rem] w-full text-base font-semibold"
                disabled={removingId === item.listingId}
                onClick={() => void handleUnfavorite(item.listingId)}
              >
                {removingId === item.listingId ? '處理中...' : '取消收藏'}
              </Button>
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
