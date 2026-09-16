import type { RefObject } from 'react'
import type { ListingCreateFormState } from '@/features/listings/api/listingApi'
import type { LookupItem } from '@/features/lookups/api/lookupApi'
import { ExpandableSelectField } from '@/shared/ui/ExpandableSelectField'
import { Input } from '@/shared/ui/Input'

export type ListingFormHighlightField =
  | 'title'
  | 'category'
  | 'condition'
  | 'residence'
  | 'pickupLocation'
  | 'price'

type ListingFormLookups = {
  categories: LookupItem[]
  conditions: LookupItem[]
  residences: LookupItem[]
  pickupLocations: LookupItem[]
}

type ListingFormFieldRefs = Partial<
  Record<Exclude<ListingFormHighlightField, never>, RefObject<HTMLDivElement | null>>
>

type Props = {
  form: ListingCreateFormState
  lookups: ListingFormLookups
  onChange: (patch: Partial<ListingCreateFormState>) => void
  highlightField?: ListingFormHighlightField | null
  fieldRefs?: ListingFormFieldRefs
  includeEmptyOptions?: boolean
}

const invalidBorderClass = '!border-2 !border-[#dc2626] transition-colors duration-300 ease-out'

const toggleButtonClass = (tone: 'blue' | 'red' | 'green', active: boolean) => {
  if (tone === 'blue') {
    return `min-h-[3.2rem] rounded-xl border px-4 py-2 text-[1.45rem] font-semibold transition ${
      active
        ? '!border-transparent !bg-[#5E5AB5] !text-white hover:!bg-[#504B9E]'
        : 'border-border bg-surface text-[#4f463f] hover:bg-surface-2'
    }`
  }
  if (tone === 'red') {
    return `min-h-[3.2rem] rounded-xl border px-4 py-2 text-[1.45rem] font-semibold transition ${
      active
        ? '!border-transparent !bg-[#B45B4D] !text-white hover:!bg-[#9F4E41]'
        : 'border-border bg-surface text-[#4f463f] hover:bg-surface-2'
    }`
  }
  return `min-h-[3.2rem] rounded-xl border px-4 py-2 text-[1.45rem] font-semibold transition ${
    active
      ? '!border-transparent !bg-[#2F7D4E] !text-white hover:!bg-[#276A43]'
      : 'border-border bg-surface text-[#4f463f] hover:bg-surface-2'
  }`
}

export const ListingFormFields = ({
  form,
  lookups,
  onChange,
  highlightField = null,
  fieldRefs,
  includeEmptyOptions = false,
}: Props) => {
  const inputHighlightClass = (field: ListingFormHighlightField) =>
    highlightField === field ? invalidBorderClass : ''
  const isSelectInvalid = (field: ListingFormHighlightField) => highlightField === field

  return (
    <>
      <div ref={fieldRefs?.title}>
        <Input
          label="標題"
          value={form.title}
          onChange={(event) => onChange({ title: event.target.value })}
          placeholder="例如：九成新電鍋"
          maxLength={80}
          className={`py-3 text-xl ${inputHighlightClass('title')}`}
          labelClassName="text-[1.45rem] font-bold text-text-main"
          required
        />
      </div>
      <label className="flex flex-col gap-2 text-lg text-text-subtle">
        <span className="text-[1.45rem] font-bold leading-tight text-text-main">描述</span>
        <textarea
          className="min-h-32 w-full rounded-xl border border-border bg-surface px-3 py-3 text-xl text-text-main outline-none transition placeholder:text-text-muted focus:border-brand"
          value={form.description}
          onChange={(event) => onChange({ description: event.target.value })}
          placeholder="補充商品狀況、使用年限、注意事項..."
          maxLength={1000}
        />
      </label>

      <div className="grid gap-3 sm:grid-cols-2">
        <div ref={fieldRefs?.category}>
          <ExpandableSelectField
            label="分類"
            value={form.categoryCode}
            options={lookups.categories}
            onChange={(value) => onChange({ categoryCode: value })}
            invalid={isSelectInvalid('category')}
            includeEmptyOption={includeEmptyOptions}
            placeholder="-"
          />
        </div>
        <div ref={fieldRefs?.condition}>
          <ExpandableSelectField
            label="品況"
            value={form.conditionCode}
            options={lookups.conditions}
            onChange={(value) => onChange({ conditionCode: value })}
            invalid={isSelectInvalid('condition')}
            includeEmptyOption={includeEmptyOptions}
            placeholder="-"
          />
        </div>
        <div ref={fieldRefs?.residence}>
          <ExpandableSelectField
            label="社宅"
            value={form.residenceCode}
            options={lookups.residences}
            onChange={(value) => onChange({ residenceCode: value })}
            invalid={isSelectInvalid('residence')}
            includeEmptyOption={includeEmptyOptions}
            placeholder="-"
          />
        </div>
        <div ref={fieldRefs?.pickupLocation}>
          <ExpandableSelectField
            label="面交地點"
            value={form.pickupLocationCode}
            options={lookups.pickupLocations}
            onChange={(value) => onChange({ pickupLocationCode: value })}
            invalid={isSelectInvalid('pickupLocation')}
            includeEmptyOption={includeEmptyOptions}
            placeholder="-"
          />
        </div>
      </div>

      <div className="grid gap-3 sm:grid-cols-3">
        <button
          type="button"
          aria-pressed={form.isFree}
          className={toggleButtonClass('green', form.isFree)}
          onClick={() => onChange({ isFree: !form.isFree, price: !form.isFree ? 0 : form.price })}
        >
          免費
        </button>
        <button
          type="button"
          aria-pressed={form.isCharity}
          className={toggleButtonClass('red', form.isCharity)}
          onClick={() => onChange({ isCharity: !form.isCharity })}
        >
          愛心捐贈
        </button>
        <button
          type="button"
          aria-pressed={form.isTradeable}
          className={toggleButtonClass('blue', form.isTradeable)}
          onClick={() => onChange({ isTradeable: !form.isTradeable })}
        >
          以物易物
        </button>
      </div>

      <div ref={fieldRefs?.price}>
        <Input
          label="價格（NT$）"
          type="number"
          value={form.price}
          min={0}
          disabled={form.isFree}
          className={`py-3 text-xl ${inputHighlightClass('price')}`}
          labelClassName="text-[1.45rem] font-bold text-text-main"
          onChange={(event) =>
            onChange({ price: Number.isNaN(Number(event.target.value)) ? 0 : Number(event.target.value) })
          }
          required={!form.isFree}
        />
      </div>
    </>
  )
}
