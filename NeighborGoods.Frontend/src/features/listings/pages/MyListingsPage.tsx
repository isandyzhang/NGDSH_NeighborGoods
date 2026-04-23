import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { listingApi, type MyListingItem } from '@/features/listings/api/listingApi'
import { ApiClientError } from '@/shared/types/api'
import { Button, getButtonClassName } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'

const statusText: Record<number, string> = {
  0: '上架中',
  1: '保留中',
  2: '已售出',
  3: '已捐贈',
  4: '已下架',
  5: '已易物',
}

export const MyListingsPage = () => {
  const [items, setItems] = useState<MyListingItem[]>([])
  const [loading, setLoading] = useState(true)
  const [busyItemId, setBusyItemId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [animatedSummary, setAnimatedSummary] = useState({
    activeCount: 0,
    soldCount: 0,
    donatedCount: 0,
  })
  const summary = useMemo(() => {
    const activeCount = items.filter((item) => item.statusCode === 0).length
    const soldCount = items.filter((item) => item.statusCode === 2).length
    const donatedCount = items.filter((item) => item.statusCode === 3 || item.statusCode === 5).length
    return { activeCount, soldCount, donatedCount }
  }, [items])

  useEffect(() => {
    const durationMs = 700
    const startTime = performance.now()
    let frameId = 0

    const animate = (now: number) => {
      const progress = Math.min((now - startTime) / durationMs, 1)
      const eased = 1 - Math.pow(1 - progress, 3)

      setAnimatedSummary({
        activeCount: Math.round(summary.activeCount * eased),
        soldCount: Math.round(summary.soldCount * eased),
        donatedCount: Math.round(summary.donatedCount * eased),
      })

      if (progress < 1) {
        frameId = window.requestAnimationFrame(animate)
      }
    }

    frameId = window.requestAnimationFrame(animate)
    return () => window.cancelAnimationFrame(frameId)
  }, [summary.activeCount, summary.soldCount, summary.donatedCount])

  useEffect(() => {
    let disposed = false

    void listingApi
      .listMine(1, 50)
      .then((data) => {
        if (!disposed) {
          setItems(data.items)
        }
      })
      .catch((err: unknown) => {
        if (!disposed) {
          setError(err instanceof ApiClientError ? err.message : '讀取我的商品失敗')
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
  }, [])

  const getAvailableActions = (statusCode: number) => {
    switch (statusCode) {
      case 0:
        return [
          { key: 'inactive', label: '下架' },
          { key: 'sold', label: '標記已售出' },
        ] as const
      case 1:
        return [
          { key: 'activate', label: '恢復上架' },
          { key: 'inactive', label: '下架' },
          { key: 'sold', label: '標記已售出' },
        ] as const
      case 4:
        return [{ key: 'reactivate', label: '重新上架' }] as const
      default:
        return [] as const
    }
  }

  const handleStatusAction = async (listingId: string, action: 'inactive' | 'sold' | 'activate' | 'reactivate') => {
    setBusyItemId(listingId)
    setError(null)
    try {
      const result = await listingApi.changeStatus(listingId, action)
      if (result.warning) {
        setError(result.warning)
      }
      const refreshed = await listingApi.listMine(1, 50)
      setItems(refreshed.items)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '更新商品狀態失敗')
    } finally {
      setBusyItemId(null)
    }
  }

  return (
    <main className="mx-auto w-full max-w-6xl px-4 py-6 md:py-8">
      <section className="mb-8 space-y-3 text-center">
        <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="text-5xl font-semibold leading-tight text-text-main sm:text-6xl md:text-7xl">
          我的<span className="marker-wipe">商品</span>
        </h1>
        <p className="mx-auto max-w-2xl text-lg text-text-subtle">管理你的刊登商品與狀態。</p>
        <div className="pt-2">
          <Link
            to="/listings/create"
            className={getButtonClassName({
              variant: 'primary',
              className: 'inline-flex min-h-[3rem] items-center justify-center px-5 text-base font-semibold',
            })}
          >
            新增刊登
          </Link>
        </div>
      </section>

      {error ? <p className="mb-4 text-sm text-danger">{error}</p> : null}
      {loading ? <Card className="h-40 animate-pulse bg-surface-2" /> : null}

      {!loading && !items.length ? (
        <EmptyState title="你目前還沒有刊登商品" description="先新增一筆商品，讓其他住戶能找到你。" />
      ) : null}

      {!loading && items.length ? (
        <>
          <p className="mb-5 text-center text-xl font-semibold leading-relaxed text-text-main sm:text-2xl">
            上架中{' '}
            <span className="stat-brush inline-block min-w-6 text-3xl font-bold tabular-nums sm:text-4xl">
              {animatedSummary.activeCount}
            </span>{' '}
            ｜ 已售出{' '}
            <span className="stat-brush inline-block min-w-6 text-3xl font-bold tabular-nums sm:text-4xl">
              {animatedSummary.soldCount}
            </span>{' '}
            ｜ 已贈與{' '}
            <span className="stat-brush inline-block min-w-6 text-3xl font-bold tabular-nums sm:text-4xl">
              {animatedSummary.donatedCount}
            </span>
          </p>

          <section className="grid grid-cols-2 gap-3 md:gap-4 lg:grid-cols-3">
            {items.map((item) => (
              (() => {
                const actions = getAvailableActions(item.statusCode)
                const editButtonClass =
                  'inline-flex min-h-[3.2rem] w-full items-center justify-center rounded-xl border-[#D8C0A3] bg-[#F3E7D8] px-1 py-1 text-xl font-semibold leading-tight hover:bg-[#EBD9C3]'

                return (
                  <Card key={item.id} className="space-y-2">
                    <div className="aspect-[4/2.4] overflow-hidden rounded-xl bg-surface-2 sm:aspect-[4/2.7]">
                      {item.mainImageUrl ? (
                        <img src={item.mainImageUrl} alt={item.title} className="h-full w-full object-cover" />
                      ) : (
                        <div className="flex h-full items-center justify-center text-sm text-text-muted">無圖片</div>
                      )}
                    </div>
                    <h2 className="line-clamp-2 text-2xl font-semibold leading-tight text-text-main md:text-2xl">{item.title}</h2>
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="inline-flex items-center rounded-full border border-[#D8C0A3] bg-[#F3E7D8] px-3 py-1 text-lg font-semibold text-text-main">
                        {item.categoryName}
                      </span>
                      <span className="inline-flex items-center rounded-full border border-[#D8C0A3] bg-[#F3E7D8] px-3 py-1 text-lg font-semibold text-text-main">
                        {item.isFree ? '免費' : `NT$ ${item.price.toLocaleString()}`}
                      </span>
                    </div>
                    <p className="text-lg text-text-subtle">狀態：{statusText[item.statusCode] ?? `狀態 ${item.statusCode}`}</p>
                    <div className="grid grid-cols-1 gap-2 pt-1">
                      <Link
                        to={`/listings/${item.id}/edit`}
                        className={getButtonClassName({
                          variant: 'secondary',
                          className: editButtonClass,
                        })}
                      >
                        編輯商品
                      </Link>
                      {actions.map((action) => (
                        <Button
                          key={action.key}
                          type="button"
                          variant="secondary"
                          className="min-h-[3.2rem] w-full rounded-xl border-[#D8C0A3] bg-[#F3E7D8] px-1 py-1 text-xl font-semibold leading-tight hover:bg-[#EBD9C3]"
                          disabled={busyItemId === item.id}
                          onClick={() => void handleStatusAction(item.id, action.key)}
                        >
                          {busyItemId === item.id ? '處理中...' : action.label}
                        </Button>
                      ))}
                    </div>
                  </Card>
                )
              })()
            ))}
          </section>
        </>
      ) : null}
    </main>
  )
}
