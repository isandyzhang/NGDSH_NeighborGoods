import {
  type Dispatch,
  type ReactNode,
  type RefObject,
  type SetStateAction,
} from 'react'
import { DotLottieReact } from '@lottiefiles/dotlottie-react'
import {
  Baby,
  BookText,
  Dumbbell,
  Gamepad2,
  Home,
  MonitorSmartphone,
  Package,
  Shirt,
  Sofa,
  UtensilsCrossed,
} from 'lucide-react'
import type { LookupItem } from '@/features/lookups/api/lookupApi'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'

export type ExpandableFilterKey = 'category' | 'condition' | 'residence'
export type QuickFilterKey = 'free' | 'charity' | 'tradeable'

const LOCAL_GIFT_LOTTIE = new URL('../../../lottie/gift icons.lottie', import.meta.url).href
const LOCAL_FAVORITES_LOTTIE = new URL('../../../lottie/Add to favorites.lottie', import.meta.url).href
const LOCAL_EXCHANGE_LOTTIE = new URL('../../../lottie/Exchange.lottie', import.meta.url).href

const QUICK_FILTER_LOTTIE: Record<QuickFilterKey, string> = {
  free: LOCAL_GIFT_LOTTIE,
  charity: LOCAL_FAVORITES_LOTTIE,
  tradeable: LOCAL_EXCHANGE_LOTTIE,
}

const getOptionIcon = (name: string) => {
  const normalized = name.trim()
  if (normalized.includes('家具')) return Sofa
  if (normalized.includes('電子')) return MonitorSmartphone
  if (normalized.includes('服飾')) return Shirt
  if (normalized.includes('書籍')) return BookText
  if (normalized.includes('運動')) return Dumbbell
  if (normalized.includes('玩具')) return Gamepad2
  if (normalized.includes('廚房')) return UtensilsCrossed
  if (normalized.includes('生活')) return Home
  if (normalized.includes('嬰幼兒')) return Baby
  return Package
}

const QuickFilterLottieIcon = ({
  filterKey,
  activeKey,
  playNonce,
  fallbackIcon,
}: {
  filterKey: QuickFilterKey
  activeKey: QuickFilterKey | null
  playNonce: number
  fallbackIcon: ReactNode
}) => (
  <span className="quick-filter-icon" aria-hidden="true">
    <span className={`quick-filter-icon-static ${activeKey === filterKey ? 'is-hidden' : ''}`}>{fallbackIcon}</span>
    {activeKey === filterKey ? (
      <span className="quick-filter-icon-lottie">
        <DotLottieReact
          key={`${filterKey}-${playNonce}`}
          src={QUICK_FILTER_LOTTIE[filterKey]}
          autoplay
          loop={false}
          style={{ width: '2.6rem', height: '2.6rem' }}
        />
      </span>
    ) : null}
  </span>
)

type Props = {
  listingSectionRef: RefObject<HTMLElement | null>
  filterSectionRef: RefObject<HTMLElement | null>
  desktopFilterAreaRef: RefObject<HTMLDivElement | null>
  desktopFilterRowRef: RefObject<HTMLDivElement | null>
  filtersInView: boolean
  categories: LookupItem[]
  conditions: LookupItem[]
  residences: LookupItem[]
  selectedCategoryCodes: number[]
  selectedConditionCodes: number[]
  selectedResidenceCodes: number[]
  setSelectedCategoryCodes: Dispatch<SetStateAction<number[]>>
  setSelectedConditionCodes: Dispatch<SetStateAction<number[]>>
  setSelectedResidenceCodes: Dispatch<SetStateAction<number[]>>
  isFree: boolean
  isCharity: boolean
  isTradeable: boolean
  setIsFree: Dispatch<SetStateAction<boolean>>
  setIsCharity: Dispatch<SetStateAction<boolean>>
  setIsTradeable: Dispatch<SetStateAction<boolean>>
  expandedFilter: ExpandableFilterKey | null
  setExpandedFilter: Dispatch<SetStateAction<ExpandableFilterKey | null>>
  mobileSheetFilter: ExpandableFilterKey | null
  setMobileSheetFilter: Dispatch<SetStateAction<ExpandableFilterKey | null>>
  quickFilterHover: QuickFilterKey | null
  quickFilterPlayNonce: number
  setPage: Dispatch<SetStateAction<number>>
  onToggleMultiCode: (value: number, setValues: Dispatch<SetStateAction<number[]>>) => void
  onClearAllFilters: () => void
  onCloseMobileFilterSheet: () => void
  onTriggerQuickFilterLottie: (key: QuickFilterKey) => void
  onClearQuickFilterLottie: () => void
}

const getFilterSummary = (title: string, selectedCodes: number[], options: LookupItem[]) => {
  if (!selectedCodes.length) {
    return title
  }

  const selectedNames = selectedCodes
    .map((code) => options.find((option) => option.id === code)?.displayName)
    .filter((name): name is string => Boolean(name))

  if (!selectedNames.length) {
    return title
  }

  if (selectedNames.length <= 2) {
    return selectedNames.join('、')
  }

  return `${selectedNames.slice(0, 2).join('、')} +${selectedNames.length - 2}`
}

const renderMultiOptions = (
  options: LookupItem[],
  selected: number[],
  onToggle: (code: number) => void,
  withIcon = false,
) => (
  <section className="space-y-3">
    <div className="grid grid-cols-2 gap-2">
      {options.map((option, index) => {
        const active = selected.includes(option.id)
        return (
          <Button
            key={option.id}
            type="button"
            onClick={() => onToggle(option.id)}
            variant="secondary"
            className={`animate-fade-in min-h-[3.6rem] w-full rounded-xl border px-3 py-2 text-xl font-semibold transition focus-visible:outline-none ${
              active
                ? '!border-[#B08F68] !bg-[#D6B897] !text-text-main shadow-[0_4px_10px_rgba(37,25,16,0.18)] hover:!bg-[#CCAB87]'
                : 'border-border bg-surface text-text-main hover:bg-surface-2'
            }`}
            style={{ animationDelay: `${index * 45}ms` }}
          >
            {withIcon ? (
              <span className="inline-flex items-center gap-2.5">
                {(() => {
                  const OptionIcon = getOptionIcon(option.displayName)
                  return <OptionIcon className="h-5 w-5" aria-hidden="true" />
                })()}
                {option.displayName}
              </span>
            ) : (
              option.displayName
            )}
          </Button>
        )
      })}
    </div>
  </section>
)

export const ListingHomeFilters = ({
  listingSectionRef,
  filterSectionRef,
  desktopFilterAreaRef,
  desktopFilterRowRef,
  filtersInView,
  categories,
  conditions,
  residences,
  selectedCategoryCodes,
  selectedConditionCodes,
  selectedResidenceCodes,
  setSelectedCategoryCodes,
  setSelectedConditionCodes,
  setSelectedResidenceCodes,
  isFree,
  isCharity,
  isTradeable,
  setIsFree,
  setIsCharity,
  setIsTradeable,
  expandedFilter,
  setExpandedFilter,
  mobileSheetFilter,
  setMobileSheetFilter,
  quickFilterHover,
  quickFilterPlayNonce,
  setPage,
  onToggleMultiCode,
  onClearAllFilters,
  onCloseMobileFilterSheet,
  onTriggerQuickFilterLottie,
  onClearQuickFilterLottie,
}: Props) => {
  const desktopFilterGroups: {
    key: ExpandableFilterKey
    title: string
    options: LookupItem[]
    selected: number[]
    setValues: Dispatch<SetStateAction<number[]>>
  }[] = [
    {
      key: 'category',
      title: '分類',
      options: categories,
      selected: selectedCategoryCodes,
      setValues: setSelectedCategoryCodes,
    },
    {
      key: 'condition',
      title: '品況',
      options: conditions,
      selected: selectedConditionCodes,
      setValues: setSelectedConditionCodes,
    },
    {
      key: 'residence',
      title: '社宅',
      options: residences,
      selected: selectedResidenceCodes,
      setValues: setSelectedResidenceCodes,
    },
  ]
  const expandedDesktopGroup = desktopFilterGroups.find((group) => group.key === expandedFilter) ?? null
  const mobileCategorySummary = getFilterSummary('分類', selectedCategoryCodes, categories)
  const mobileConditionSummary = getFilterSummary('品況', selectedConditionCodes, conditions)
  const mobileResidenceSummary = getFilterSummary('社宅', selectedResidenceCodes, residences)
  const mobileCategoryActive = mobileSheetFilter === 'category' || selectedCategoryCodes.length > 0
  const mobileConditionActive = mobileSheetFilter === 'condition' || selectedConditionCodes.length > 0
  const mobileResidenceActive = mobileSheetFilter === 'residence' || selectedResidenceCodes.length > 0
  const activeFilterCount =
    selectedCategoryCodes.length +
    selectedConditionCodes.length +
    selectedResidenceCodes.length +
    Number(isFree) +
    Number(isCharity) +
    Number(isTradeable)

  return (
    <>
      <section ref={listingSectionRef} aria-label="商品篩選起點" className="h-0 scroll-mt-28" />
      <section
        ref={filterSectionRef}
        className="animate-fade-rise mb-6 scroll-mt-28 space-y-4"
        style={{ animationDelay: '360ms' }}
      >
        <div ref={desktopFilterAreaRef} className="hidden space-y-4 md:block">
          <div ref={desktopFilterRowRef} className="flex items-start justify-between gap-5 md:flex-nowrap">
            <div className="grid flex-1 grid-cols-3 items-start gap-5">
              {desktopFilterGroups.map((group) => {
                const isExpanded = expandedFilter === group.key
                const hasSelectedValue = group.selected.length > 0
                const flyInClass =
                  group.key === 'category' ? 'fi-1' : group.key === 'condition' ? 'fi-2' : 'fi-3'
                return (
                  <div key={group.key} className="min-w-0">
                    <Button
                      type="button"
                      variant="secondary"
                      className={`filter-trigger filter-fly-in ${flyInClass} ${filtersInView ? 'is-visible' : ''} flex h-[4.2rem] w-full items-center justify-center rounded-[999px] border px-6 text-[1.3rem] font-semibold shadow-soft transition ${
                        isExpanded || hasSelectedValue
                          ? '!border-[#B08F68] !bg-[#D6B897] !text-text-main shadow-[0_4px_10px_rgba(37,25,16,0.18)] hover:!bg-[#CCAB87]'
                          : 'border-border bg-surface text-text-main hover:bg-surface-2'
                      }`}
                      onClick={() => setExpandedFilter((current) => (current === group.key ? null : group.key))}
                    >
                      <span className="filter-trigger-icon" aria-hidden="true">
                        <svg viewBox="0 0 24 24" className="h-7 w-7" fill="none" stroke="currentColor" strokeWidth="2.2">
                          <circle cx="11" cy="11" r="6.5" />
                          <path d="M16 16L21 21" strokeLinecap="round" />
                        </svg>
                      </span>
                      <span className="filter-trigger-label truncate">
                        {getFilterSummary(group.title, group.selected, group.options)}
                      </span>
                    </Button>
                  </div>
                )
              })}
            </div>

            <div className="flex shrink-0 flex-nowrap justify-end gap-5">
              <Button
                type="button"
                onMouseEnter={() => onTriggerQuickFilterLottie('free')}
                onFocus={() => onTriggerQuickFilterLottie('free')}
                onMouseLeave={onClearQuickFilterLottie}
                onBlur={onClearQuickFilterLottie}
                onClick={() => {
                  setPage(1)
                  setIsFree((current) => !current)
                }}
                variant="secondary"
                className={`filter-fly-in fi-4 ${filtersInView ? 'is-visible' : ''} h-[4.2rem] min-w-[8.6rem] rounded-[9999px] border px-6 text-[1.3rem] font-semibold shadow-soft transition active:scale-[0.98] ${
                  isFree
                    ? '!border-[#2F7D4E] !bg-[#2F7D4E] !text-white hover:!bg-[#276942]'
                    : 'border-border bg-surface text-text-main hover:bg-surface-2'
                }`}
              >
                <span className="inline-flex items-center gap-2">
                  <QuickFilterLottieIcon
                    filterKey="free"
                    activeKey={quickFilterHover}
                    playNonce={quickFilterPlayNonce}
                    fallbackIcon={
                      <svg viewBox="0 0 24 24" className="h-7 w-7" fill="none" stroke="currentColor" strokeWidth="2">
                        <rect x="4" y="9" width="16" height="11" rx="2" />
                        <path d="M12 9V20M4 13H20M7.5 9C6.3 9 5.5 8.3 5.5 7.3C5.5 6.1 6.5 5.4 7.5 5.4C9.4 5.4 10.8 7.2 12 9M16.5 9C17.7 9 18.5 8.3 18.5 7.3C18.5 6.1 17.5 5.4 16.5 5.4C14.6 5.4 13.2 7.2 12 9" />
                      </svg>
                    }
                  />
                  免費
                </span>
              </Button>
              <Button
                type="button"
                onMouseEnter={() => onTriggerQuickFilterLottie('charity')}
                onFocus={() => onTriggerQuickFilterLottie('charity')}
                onMouseLeave={onClearQuickFilterLottie}
                onBlur={onClearQuickFilterLottie}
                onClick={() => {
                  setPage(1)
                  setIsCharity((current) => !current)
                }}
                variant="secondary"
                className={`filter-fly-in fi-5 ${filtersInView ? 'is-visible' : ''} h-[4.2rem] min-w-[8.6rem] rounded-[9999px] border px-6 text-[1.3rem] font-semibold shadow-soft transition active:scale-[0.98] ${
                  isCharity
                    ? '!border-[#B45B4D] !bg-[#B45B4D] !text-white hover:!bg-[#984B40]'
                    : 'border-border bg-surface text-text-main hover:bg-surface-2'
                }`}
              >
                <span className="inline-flex items-center gap-2">
                  <QuickFilterLottieIcon
                    filterKey="charity"
                    activeKey={quickFilterHover}
                    playNonce={quickFilterPlayNonce}
                    fallbackIcon={
                      <svg viewBox="0 0 24 24" className="h-7 w-7" fill="none" stroke="currentColor" strokeWidth="2">
                        <path d="M12 20.4C11.2 19.7 4.5 14.2 4.5 9.4C4.5 7.1 6.3 5.3 8.6 5.3C10 5.3 11.2 6 12 7.1C12.8 6 14 5.3 15.4 5.3C17.7 5.3 19.5 7.1 19.5 9.4C19.5 14.2 12.8 19.7 12 20.4Z" />
                      </svg>
                    }
                  />
                  愛心捐贈
                </span>
              </Button>
              <Button
                type="button"
                onMouseEnter={() => onTriggerQuickFilterLottie('tradeable')}
                onFocus={() => onTriggerQuickFilterLottie('tradeable')}
                onMouseLeave={onClearQuickFilterLottie}
                onBlur={onClearQuickFilterLottie}
                onClick={() => {
                  setPage(1)
                  setIsTradeable((current) => !current)
                }}
                variant="secondary"
                className={`filter-fly-in fi-6 ${filtersInView ? 'is-visible' : ''} h-[4.2rem] min-w-[8.6rem] rounded-[9999px] border px-6 text-[1.3rem] font-semibold shadow-soft transition active:scale-[0.98] ${
                  isTradeable
                    ? '!border-[#5E5AB5] !bg-[#5E5AB5] !text-white hover:!bg-[#4F4B99]'
                    : 'border-border bg-surface text-text-main hover:bg-surface-2'
                }`}
              >
                <span className="inline-flex items-center gap-2">
                  <QuickFilterLottieIcon
                    filterKey="tradeable"
                    activeKey={quickFilterHover}
                    playNonce={quickFilterPlayNonce}
                    fallbackIcon={
                      <svg viewBox="0 0 24 24" className="h-7 w-7" fill="none" stroke="currentColor" strokeWidth="2">
                        <path d="M6 8H18M14.5 4.5L18 8L14.5 11.5M18 16H6M9.5 12.5L6 16L9.5 19.5" strokeLinecap="round" strokeLinejoin="round" />
                      </svg>
                    }
                  />
                  以物易物
                </span>
              </Button>
              <Button
                type="button"
                variant="secondary"
                onClick={onClearAllFilters}
                className={`filter-fly-in fi-7 ${filtersInView ? 'is-visible' : ''} h-[4.2rem] rounded-[999px] px-6 text-[1.3rem] font-semibold whitespace-nowrap`}
              >
                清除條件 {activeFilterCount ? `(${activeFilterCount})` : ''}
              </Button>
            </div>
          </div>
          <div
            className={`overflow-hidden transition-all duration-300 ease-out ${
              expandedDesktopGroup ? 'mt-1 max-h-72 overflow-visible opacity-100' : 'max-h-0 overflow-hidden opacity-0'
            }`}
          >
            {expandedDesktopGroup ? (
              <section>
                <div className="hide-scrollbar flex items-center gap-3 overflow-x-auto overflow-y-visible py-2">
                  {expandedDesktopGroup.options.map((option, index) => {
                    const active = expandedDesktopGroup.selected.includes(option.id)
                    return (
                      <Button
                        key={option.id}
                        type="button"
                        onClick={() => onToggleMultiCode(option.id, expandedDesktopGroup.setValues)}
                        variant="secondary"
                        className={`option-chip-enter shrink-0 rounded-2xl border px-6 py-3 text-[1.2rem] font-semibold transition focus-visible:outline-none ${
                          active
                            ? '!border-[#B08F68] !bg-[#D6B897] !text-text-main shadow-[0_4px_10px_rgba(37,25,16,0.18)] hover:!bg-[#CCAB87]'
                            : 'border-border bg-surface text-text-main hover:bg-surface-2'
                        }`}
                        style={{ animationDelay: `${index * 55}ms` }}
                      >
                        {expandedDesktopGroup.key === 'category' ? (
                          <span className="inline-flex items-center gap-2.5">
                            {(() => {
                              const OptionIcon = getOptionIcon(option.displayName)
                              return <OptionIcon className="h-5 w-5" aria-hidden="true" />
                            })()}
                            {option.displayName}
                          </span>
                        ) : (
                          option.displayName
                        )}
                      </Button>
                    )
                  })}
                </div>
              </section>
            ) : null}
          </div>
        </div>

        <div className="grid grid-cols-3 gap-2 md:hidden">
          <Button
            type="button"
            variant="secondary"
            className={`h-14 rounded-[999px] px-2 text-xl font-semibold shadow-soft ${
              mobileCategoryActive
                ? '!border-brand !bg-brand !text-brand-foreground hover:!bg-brand-strong'
                : 'border-border bg-surface text-text-main hover:bg-surface-2'
            }`}
            onClick={() => setMobileSheetFilter('category')}
          >
            <span className="block truncate px-1">{mobileCategorySummary}</span>
          </Button>
          <Button
            type="button"
            variant="secondary"
            className={`h-14 rounded-[999px] px-2 text-xl font-semibold shadow-soft ${
              mobileConditionActive
                ? '!border-brand !bg-brand !text-brand-foreground hover:!bg-brand-strong'
                : 'border-border bg-surface text-text-main hover:bg-surface-2'
            }`}
            onClick={() => setMobileSheetFilter('condition')}
          >
            <span className="block truncate px-1">{mobileConditionSummary}</span>
          </Button>
          <Button
            type="button"
            variant="secondary"
            className={`h-14 rounded-[999px] px-2 text-xl font-semibold shadow-soft ${
              mobileResidenceActive
                ? '!border-brand !bg-brand !text-brand-foreground hover:!bg-brand-strong'
                : 'border-border bg-surface text-text-main hover:bg-surface-2'
            }`}
            onClick={() => setMobileSheetFilter('residence')}
          >
            <span className="block truncate px-1">{mobileResidenceSummary}</span>
          </Button>
        </div>

        <div className="grid grid-cols-3 gap-2 md:hidden">
          <Button
            type="button"
            variant="secondary"
            onClick={() => {
              setPage(1)
              setIsFree((current) => !current)
            }}
            className={`h-14 rounded-[999px] border px-2 text-xl font-semibold whitespace-nowrap shadow-soft transition active:scale-[0.98] ${
              isFree
                ? '!border-[#2F7D4E] !bg-[#2F7D4E] !text-white hover:!bg-[#276942]'
                : 'border-border bg-surface text-text-main'
            }`}
          >
            免費
          </Button>
          <Button
            type="button"
            variant="secondary"
            onClick={() => {
              setPage(1)
              setIsCharity((current) => !current)
            }}
            className={`h-14 rounded-[999px] border px-2 text-xl font-semibold whitespace-nowrap shadow-soft transition active:scale-[0.98] ${
              isCharity
                ? '!border-[#B45B4D] !bg-[#B45B4D] !text-white hover:!bg-[#984B40]'
                : 'border-border bg-surface text-text-main'
            }`}
          >
            愛心捐贈
          </Button>
          <Button
            type="button"
            variant="secondary"
            onClick={() => {
              setPage(1)
              setIsTradeable((current) => !current)
            }}
            className={`h-14 rounded-[999px] border px-2 text-xl font-semibold whitespace-nowrap shadow-soft transition active:scale-[0.98] ${
              isTradeable
                ? '!border-[#5E5AB5] !bg-[#5E5AB5] !text-white hover:!bg-[#4F4B99]'
                : 'border-border bg-surface text-text-main'
            }`}
          >
            以物易物
          </Button>
          <Button
            type="button"
            variant="secondary"
            onClick={onClearAllFilters}
            className="col-span-3 h-14 rounded-[999px] text-xl font-semibold"
          >
            清除條件 {activeFilterCount ? `(${activeFilterCount})` : ''}
          </Button>
        </div>
      </section>

      {mobileSheetFilter ? (
        <div className="fixed inset-0 z-20 flex items-end bg-black/35 pb-3 md:hidden">
          <button
            type="button"
            className="absolute inset-0"
            aria-label="關閉條件選單"
            onClick={onCloseMobileFilterSheet}
          />
          <Card className="animate-sheet-up relative z-10 mx-auto flex h-[62vh] w-[calc(100%-1rem)] flex-col overflow-auto rounded-2xl p-0">
            <div className="sticky top-0 z-10 mb-4 flex items-center justify-between rounded-t-2xl border-b border-border bg-[#F3E7D8] px-4 py-3">
              <h3 className="text-3xl font-semibold text-text-main">
                {mobileSheetFilter === 'category'
                  ? '選擇分類'
                  : mobileSheetFilter === 'condition'
                    ? '選擇品況'
                    : '選擇社宅'}
              </h3>
              <Button
                type="button"
                variant="secondary"
                className="min-h-[3.3rem] px-5 text-xl font-semibold"
                onClick={onCloseMobileFilterSheet}
              >
                完成
              </Button>
            </div>
            <div className="px-3 pb-4">
              {mobileSheetFilter === 'category'
                ? renderMultiOptions(categories, selectedCategoryCodes, (code) =>
                    onToggleMultiCode(code, setSelectedCategoryCodes),
                  true,
                )
                : null}
              {mobileSheetFilter === 'condition'
                ? renderMultiOptions(conditions, selectedConditionCodes, (code) =>
                    onToggleMultiCode(code, setSelectedConditionCodes),
                  )
                : null}
              {mobileSheetFilter === 'residence'
                ? renderMultiOptions(residences, selectedResidenceCodes, (code) =>
                    onToggleMultiCode(code, setSelectedResidenceCodes),
                  )
                : null}
            </div>
          </Card>
        </div>
      ) : null}
    </>
  )
}
