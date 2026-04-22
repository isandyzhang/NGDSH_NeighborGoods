import { useEffect, useMemo, useState, type Dispatch, type SetStateAction } from 'react'
import { Link } from 'react-router-dom'
import { lookupApi, type LookupItem } from '@/features/lookups/api/lookupApi'
import { listingApi, type ListingItem } from '@/features/listings/api/listingApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'

const PAGE_SIZE = 12
const MIN_SKELETON_MS = 180

type ExpandableFilterKey = 'category' | 'condition' | 'residence'

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
  const [selectedCategoryCodes, setSelectedCategoryCodes] = useState<number[]>([])
  const [selectedConditionCodes, setSelectedConditionCodes] = useState<number[]>([])
  const [selectedResidenceCodes, setSelectedResidenceCodes] = useState<number[]>([])
  const [isFree, setIsFree] = useState(false)
  const [isCharity, setIsCharity] = useState(false)
  const [isTradeable, setIsTradeable] = useState(false)
  const [expandedFilter, setExpandedFilter] = useState<ExpandableFilterKey | null>(null)
  const [mobileSheetFilter, setMobileSheetFilter] = useState<ExpandableFilterKey | null>(null)
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
    const startedAt = Date.now()
    setLoading(true)
    setError(null)

    void listingApi
      .list({
        page,
        pageSize: PAGE_SIZE,
        categoryCodes: selectedCategoryCodes,
        conditionCodes: selectedConditionCodes,
        residenceCodes: selectedResidenceCodes,
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
        const elapsed = Date.now() - startedAt
        const wait = Math.max(0, MIN_SKELETON_MS - elapsed)
        window.setTimeout(() => {
          if (!disposed) {
            setLoading(false)
          }
        }, wait)
      })

    return () => {
      disposed = true
    }
  }, [
    page,
    selectedCategoryCodes,
    selectedConditionCodes,
    selectedResidenceCodes,
    isFree,
    isCharity,
    isTradeable,
  ])

  const summaryText = useMemo(() => {
    if (loading) {
      return '載入中...'
    }
    return '依社宅與條件快速篩選可交易商品'
  }, [loading])

  const toggleMultiCode = (
    value: number,
    setValues: Dispatch<SetStateAction<number[]>>,
  ) => {
    setPage(1)
    setValues((current) =>
      current.includes(value) ? current.filter((x) => x !== value) : [...current, value],
    )
  }

  const activeFilterCount =
    selectedCategoryCodes.length +
    selectedConditionCodes.length +
    selectedResidenceCodes.length +
    Number(isFree) +
    Number(isCharity) +
    Number(isTradeable)

  const clearAllFilters = () => {
    setPage(1)
    setSelectedCategoryCodes([])
    setSelectedConditionCodes([])
    setSelectedResidenceCodes([])
    setIsFree(false)
    setIsCharity(false)
    setIsTradeable(false)
    setExpandedFilter(null)
    setMobileSheetFilter(null)
  }

  const renderMultiOptions = (
    title: string,
    options: LookupItem[],
    selected: number[],
    onToggle: (code: number) => void,
  ) => (
    <section className="space-y-3">
      <h3 className="text-sm font-medium text-text-main">{title}</h3>
      <div className="flex flex-wrap gap-2">
        {options.map((option) => {
          const active = selected.includes(option.id)
          return (
            <button
              key={option.id}
              type="button"
              onClick={() => onToggle(option.id)}
              className={`rounded-full border px-3 py-1.5 text-sm transition ${
                active
                  ? 'border-brand bg-brand text-brand-foreground shadow-soft'
                  : 'border-border bg-surface text-text-main hover:bg-surface-2'
              }`}
            >
              {option.displayName}
            </button>
          )
        })}
      </div>
    </section>
  )

  return (
    <main className="mx-auto w-full max-w-6xl px-4 py-6 md:py-8">
      <section className="mb-6 space-y-3">
        <p className="animate-fade-rise text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1
          className="animate-fade-rise text-3xl font-semibold leading-tight text-text-main sm:text-4xl md:text-5xl"
          style={{ animationDelay: '80ms' }}
        >
          社宅專屬二手交易平台
        </h1>
        <p className="animate-fade-rise text-text-subtle" style={{ animationDelay: '140ms' }}>
          {summaryText}
        </p>
      </section>

      <Card className="animate-fade-rise mb-6 space-y-3" style={{ animationDelay: '220ms' }}>
        <div className="hidden gap-2 md:flex md:flex-wrap">
          <Button
            type="button"
            className="shadow-soft"
            onClick={() => setExpandedFilter((current) => (current === 'category' ? null : 'category'))}
          >
            分類 {selectedCategoryCodes.length ? `(${selectedCategoryCodes.length})` : ''}
          </Button>
          <Button
            type="button"
            className="shadow-soft"
            onClick={() => setExpandedFilter((current) => (current === 'condition' ? null : 'condition'))}
          >
            品況 {selectedConditionCodes.length ? `(${selectedConditionCodes.length})` : ''}
          </Button>
          <Button
            type="button"
            className="shadow-soft"
            onClick={() => setExpandedFilter((current) => (current === 'residence' ? null : 'residence'))}
          >
            社宅 {selectedResidenceCodes.length ? `(${selectedResidenceCodes.length})` : ''}
          </Button>
        </div>

        <div className="grid grid-cols-3 gap-2 md:hidden">
          <Button type="button" className="shadow-soft text-xs" onClick={() => setMobileSheetFilter('category')}>
            分類
          </Button>
          <Button type="button" className="shadow-soft text-xs" onClick={() => setMobileSheetFilter('condition')}>
            品況
          </Button>
          <Button type="button" className="shadow-soft text-xs" onClick={() => setMobileSheetFilter('residence')}>
            社宅
          </Button>
        </div>

        {expandedFilter === 'category' ? (
          <div className="animate-expand-fade hidden md:block">
            {renderMultiOptions('分類', categories, selectedCategoryCodes, (code) =>
              toggleMultiCode(code, setSelectedCategoryCodes),
            )}
          </div>
        ) : null}
        {expandedFilter === 'condition' ? (
          <div className="animate-expand-fade hidden md:block">
            {renderMultiOptions('品況', conditions, selectedConditionCodes, (code) =>
              toggleMultiCode(code, setSelectedConditionCodes),
            )}
          </div>
        ) : null}
        {expandedFilter === 'residence' ? (
          <div className="animate-expand-fade hidden md:block">
            {renderMultiOptions('社宅', residences, selectedResidenceCodes, (code) =>
              toggleMultiCode(code, setSelectedResidenceCodes),
            )}
          </div>
        ) : null}

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => {
              setPage(1)
              setIsFree((current) => !current)
            }}
            className={`rounded-full border px-4 py-2 text-sm shadow-soft transition active:scale-[0.98] ${
              isFree
                ? 'border-[#2F7D4E] bg-[#2F7D4E] text-white'
                : 'border-[#BFDCC9] bg-[#E7F4EA] text-[#205A3A]'
            }`}
          >
            免費
          </button>
          <button
            type="button"
            onClick={() => {
              setPage(1)
              setIsCharity((current) => !current)
            }}
            className={`rounded-full border px-4 py-2 text-sm shadow-soft transition active:scale-[0.98] ${
              isCharity
                ? 'border-[#B45B4D] bg-[#B45B4D] text-white'
                : 'border-[#EAC8C2] bg-[#FBECEA] text-[#7F3D34]'
            }`}
          >
            愛心捐贈
          </button>
          <button
            type="button"
            onClick={() => {
              setPage(1)
              setIsTradeable((current) => !current)
            }}
            className={`rounded-full border px-4 py-2 text-sm shadow-soft transition active:scale-[0.98] ${
              isTradeable
                ? 'border-[#5E5AB5] bg-[#5E5AB5] text-white'
                : 'border-[#CFCBEA] bg-[#ECEAF9] text-[#484587]'
            }`}
          >
            以物易物
          </button>
          <Button type="button" variant="secondary" onClick={clearAllFilters}>
            清除條件 {activeFilterCount ? `(${activeFilterCount})` : ''}
          </Button>
        </div>
      </Card>

      {mobileSheetFilter ? (
        <div className="fixed inset-0 z-20 flex items-end bg-black/35 md:hidden">
          <button
            type="button"
            className="absolute inset-0"
            aria-label="關閉條件選單"
            onClick={() => setMobileSheetFilter(null)}
          />
          <Card className="animate-sheet-up relative z-10 max-h-[70vh] w-full overflow-auto rounded-b-none">
            <div className="mb-3 flex items-center justify-between">
              <h3 className="text-base font-semibold text-text-main">
                {mobileSheetFilter === 'category'
                  ? '選擇分類'
                  : mobileSheetFilter === 'condition'
                    ? '選擇品況'
                    : '選擇社宅'}
              </h3>
              <Button type="button" variant="secondary" onClick={() => setMobileSheetFilter(null)}>
                完成
              </Button>
            </div>
            {mobileSheetFilter === 'category'
              ? renderMultiOptions('分類', categories, selectedCategoryCodes, (code) =>
                  toggleMultiCode(code, setSelectedCategoryCodes),
                )
              : null}
            {mobileSheetFilter === 'condition'
              ? renderMultiOptions('品況', conditions, selectedConditionCodes, (code) =>
                  toggleMultiCode(code, setSelectedConditionCodes),
                )
              : null}
            {mobileSheetFilter === 'residence'
              ? renderMultiOptions('社宅', residences, selectedResidenceCodes, (code) =>
                  toggleMultiCode(code, setSelectedResidenceCodes),
                )
              : null}
          </Card>
        </div>
      ) : null}

      {error ? <p className="mb-4 text-sm text-danger">{error}</p> : null}

      {loading ? (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {Array.from({ length: 6 }).map((_, index) => (
            <Card key={index} className="h-56 animate-pulse bg-surface-2" />
          ))}
        </div>
      ) : null}

      {!loading && !items.length ? (
        <EmptyState title="目前沒有符合條件的商品" description="請調整篩選條件，或稍後再試一次。" />
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
