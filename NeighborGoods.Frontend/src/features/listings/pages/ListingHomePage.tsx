import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { lookupApi, type LookupItem } from '@/features/lookups/api/lookupApi'
import { listingApi, type ListingItem } from '@/features/listings/api/listingApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { Input } from '@/shared/ui/Input'

const PAGE_SIZE = 12

const formatPrice = (item: ListingItem) => {
  if (item.isFree) {
    return '免費'
  }

  return `NT$ ${item.price.toLocaleString()}`
}

export const ListingHomePage = () => {
  const [items, setItems] = useState<ListingItem[]>([])
  const [categories, setCategories] = useState<LookupItem[]>([])
  const [conditions, setConditions] = useState<LookupItem[]>([])
  const [residences, setResidences] = useState<LookupItem[]>([])
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [searchInput, setSearchInput] = useState('')
  const [query, setQuery] = useState('')
  const [categoryCode, setCategoryCode] = useState('')
  const [conditionCode, setConditionCode] = useState('')
  const [residenceCode, setResidenceCode] = useState('')
  const [minPrice, setMinPrice] = useState('')
  const [maxPrice, setMaxPrice] = useState('')
  const [isFree, setIsFree] = useState(false)
  const [isCharity, setIsCharity] = useState(false)
  const [isTradeable, setIsTradeable] = useState(false)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let disposed = false

    void Promise.all([lookupApi.categories(), lookupApi.conditions(), lookupApi.residences()])
      .then(([categoryResult, conditionResult, residenceResult]) => {
        if (disposed) {
          return
        }

        setCategories(categoryResult)
        setConditions(conditionResult)
        setResidences(residenceResult)
      })
      .catch(() => {
        if (!disposed) {
          setError('載入篩選條件失敗，請稍後再試')
        }
      })

    return () => {
      disposed = true
    }
  }, [])

  useEffect(() => {
    let disposed = false
    setLoading(true)
    setError(null)

    void listingApi
      .list({
        page,
        pageSize: PAGE_SIZE,
        q: query || undefined,
        categoryCode: categoryCode ? Number(categoryCode) : undefined,
        conditionCode: conditionCode ? Number(conditionCode) : undefined,
        residenceCode: residenceCode ? Number(residenceCode) : undefined,
        minPrice: minPrice ? Number(minPrice) : undefined,
        maxPrice: maxPrice ? Number(maxPrice) : undefined,
        isFree: isFree || undefined,
        isCharity: isCharity || undefined,
        isTradeable: isTradeable || undefined,
      })
      .then((data) => {
        if (disposed) {
          return
        }

        setItems(data.items)
        setTotalPages(data.pagination.totalPages || 1)
      })
      .catch((err: unknown) => {
        if (disposed) {
          return
        }

        const message = err instanceof ApiClientError ? err.message : '讀取商品列表失敗'
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
  }, [page, query, categoryCode, conditionCode, residenceCode, minPrice, maxPrice, isFree, isCharity, isTradeable])

  const summaryText = useMemo(() => {
    if (loading) {
      return '載入中...'
    }
    if (query) {
      return `搜尋關鍵字：${query}`
    }
    return '探索社區最新刊登'
  }, [loading, query])

  const handleSearch = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setPage(1)
    setQuery(searchInput.trim())
  }

  return (
    <main className="mx-auto w-full max-w-6xl px-4 py-6 md:py-8">
      <section className="mb-6 space-y-3">
        <h1 className="text-3xl font-semibold leading-tight text-text-main sm:text-4xl md:text-5xl">
          鄰里好物交換與贈與
        </h1>
        <p className="text-text-subtle">{summaryText}</p>
      </section>

      <Card className="mb-6">
        <form className="space-y-3" onSubmit={handleSearch}>
          <Input
            label="搜尋標題或描述"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder="例如：嬰兒床、書桌、單車"
          />
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            <label className="text-sm text-text-subtle">
              分類
              <select
                className="mt-1 w-full rounded-xl border border-border bg-surface px-3 py-2 text-text-main"
                value={categoryCode}
                onChange={(event) => setCategoryCode(event.target.value)}
              >
                <option value="">全部分類</option>
                {categories.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.displayName}
                  </option>
                ))}
              </select>
            </label>
            <label className="text-sm text-text-subtle">
              品況
              <select
                className="mt-1 w-full rounded-xl border border-border bg-surface px-3 py-2 text-text-main"
                value={conditionCode}
                onChange={(event) => setConditionCode(event.target.value)}
              >
                <option value="">全部品況</option>
                {conditions.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.displayName}
                  </option>
                ))}
              </select>
            </label>
            <label className="text-sm text-text-subtle sm:col-span-2 lg:col-span-1">
              社宅
              <select
                className="mt-1 w-full rounded-xl border border-border bg-surface px-3 py-2 text-text-main"
                value={residenceCode}
                onChange={(event) => setResidenceCode(event.target.value)}
              >
                <option value="">全部社宅</option>
                {residences.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.displayName}
                  </option>
                ))}
              </select>
            </label>
            <label className="text-sm text-text-subtle">
              最低價格
              <input
                type="number"
                min={0}
                className="mt-1 w-full rounded-xl border border-border bg-surface px-3 py-2 text-text-main"
                value={minPrice}
                onChange={(event) => setMinPrice(event.target.value)}
                placeholder="0"
              />
            </label>
            <label className="text-sm text-text-subtle">
              最高價格
              <input
                type="number"
                min={0}
                className="mt-1 w-full rounded-xl border border-border bg-surface px-3 py-2 text-text-main"
                value={maxPrice}
                onChange={(event) => setMaxPrice(event.target.value)}
                placeholder="不限"
              />
            </label>
            <div className="flex items-end">
              <Button type="submit" fullWidth>
                搜尋
              </Button>
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-3 text-sm text-text-subtle">
            <label className="flex items-center gap-2">
              <input type="checkbox" checked={isFree} onChange={(event) => setIsFree(event.target.checked)} />
              免費
            </label>
            <label className="flex items-center gap-2">
              <input
                type="checkbox"
                checked={isCharity}
                onChange={(event) => setIsCharity(event.target.checked)}
              />
              愛心捐贈
            </label>
            <label className="flex items-center gap-2">
              <input
                type="checkbox"
                checked={isTradeable}
                onChange={(event) => setIsTradeable(event.target.checked)}
              />
              以物易物
            </label>
            <Button
              type="button"
              variant="secondary"
              onClick={() => {
                setSearchInput('')
                setQuery('')
                setCategoryCode('')
                setConditionCode('')
                setResidenceCode('')
                setMinPrice('')
                setMaxPrice('')
                setIsFree(false)
                setIsCharity(false)
                setIsTradeable(false)
                setPage(1)
              }}
            >
              清除條件
            </Button>
          </div>
        </form>
      </Card>

      {error ? <p className="mb-4 text-sm text-danger">{error}</p> : null}

      {loading ? (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {Array.from({ length: 6 }).map((_, index) => (
            <Card key={index} className="h-56 animate-pulse bg-surface-2" />
          ))}
        </div>
      ) : null}

      {!loading && !items.length ? (
        <EmptyState title="目前沒有符合條件的商品" description="請調整搜尋條件，或稍後再試一次。" />
      ) : null}

      {!loading && items.length ? (
        <>
          <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {items.map((item) => (
              <Card key={item.id} className="flex h-full flex-col gap-3">
                <div className="aspect-[4/3] overflow-hidden rounded-xl bg-surface-2">
                  {item.mainImageUrl ? (
                    <img src={item.mainImageUrl} alt={item.title} className="h-full w-full object-cover" />
                  ) : (
                    <div className="flex h-full items-center justify-center text-sm text-text-muted">無圖片</div>
                  )}
                </div>
                <div className="space-y-1">
                  <h2 className="line-clamp-2 text-lg font-semibold text-text-main">{item.title}</h2>
                  <p className="text-sm text-text-subtle">
                    {item.categoryName}・{item.conditionName}
                  </p>
                </div>
                <div className="mt-auto flex items-center justify-between text-sm">
                  <span className="font-semibold text-text-main">{formatPrice(item)}</span>
                  <span className="text-text-muted">收藏 {item.interestCount}</span>
                </div>
                <div className="pt-1">
                  <Link
                    to={`/listings/${item.id}`}
                    className="inline-flex rounded-lg border border-border px-3 py-1.5 text-sm text-text-main transition hover:bg-surface-2"
                  >
                    查看詳情
                  </Link>
                </div>
              </Card>
            ))}
          </section>
          <footer className="mt-6 flex flex-col items-center justify-between gap-3 sm:flex-row">
            <Button variant="secondary" disabled={page <= 1} onClick={() => setPage((current) => current - 1)}>
              上一頁
            </Button>
            <span className="text-sm text-text-subtle">
              第 {page} / {totalPages} 頁
            </span>
            <Button
              variant="secondary"
              disabled={page >= totalPages}
              onClick={() => setPage((current) => current + 1)}
            >
              下一頁
            </Button>
          </footer>
        </>
      ) : null}
    </main>
  )
}
