import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { listingApi, type ListingDetail } from '@/features/listings/api/listingApi'
import { ApiClientError } from '@/shared/types/api'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'

const formatPrice = (item: ListingDetail) => {
  if (item.isFree) {
    return '免費'
  }

  return `NT$ ${item.price.toLocaleString()}`
}

export const ListingDetailPage = () => {
  const { id = '' } = useParams()
  const [item, setItem] = useState<ListingDetail | null>(null)
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

  return (
    <main className="mx-auto w-full max-w-6xl px-4 py-6 md:py-8">
      <Link to="/listings" className="text-sm text-text-subtle hover:text-text-main">
        ← 返回商品列表
      </Link>

      {loading ? <Card className="mt-4 h-80 animate-pulse bg-surface-2" /> : null}
      {error ? <p className="mt-4 text-sm text-danger">{error}</p> : null}

      {!loading && !error && !item ? (
        <div className="mt-4">
          <EmptyState title="查無商品" description="這筆商品可能已下架或不存在。" />
        </div>
      ) : null}

      {item ? (
        <section className="mt-4 grid gap-4 md:grid-cols-2">
          <Card className="p-0">
            <div className="aspect-[4/3] overflow-hidden rounded-2xl bg-surface-2">
              {item.mainImageUrl ? (
                <img src={item.mainImageUrl} alt={item.title} className="h-full w-full object-cover" />
              ) : (
                <div className="flex h-full items-center justify-center text-sm text-text-muted">無圖片</div>
              )}
            </div>
          </Card>
          <Card className="space-y-3">
            <h1 className="text-2xl font-semibold text-text-main md:text-3xl">{item.title}</h1>
            <p className="text-xl font-semibold text-text-main">{formatPrice(item)}</p>
            <p className="text-sm text-text-subtle">
              {item.categoryName}・{item.conditionName}・{item.residenceName}
            </p>
            <p className="text-sm text-text-subtle">面交地點：{item.pickupLocationName}</p>
            <p className="whitespace-pre-wrap text-sm leading-6 text-text-main">
              {item.description || '賣家尚未提供描述。'}
            </p>
          </Card>
        </section>
      ) : null}
    </main>
  )
}
