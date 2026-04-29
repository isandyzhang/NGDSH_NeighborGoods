import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { listingApi, type ListingMutationPayload } from '@/features/listings/api/listingApi'
import { lookupApi, type LookupItem } from '@/features/lookups/api/lookupApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { ExpandableSelectField } from '@/shared/ui/ExpandableSelectField'
import { Input } from '@/shared/ui/Input'

const invalidBorderClass = '!border-2 !border-[#dc2626] transition-colors duration-300 ease-out'

const defaultForm: ListingMutationPayload = {
  title: '',
  description: '',
  categoryCode: 0,
  conditionCode: 0,
  price: 0,
  residenceCode: 0,
  pickupLocationCode: 0,
  isFree: false,
  isCharity: false,
  isTradeable: false,
}

type ValidationField =
  | 'title'
  | 'category'
  | 'condition'
  | 'residence'
  | 'pickupLocation'
  | 'price'
  | 'images'


export const CreateListingPage = () => {
  const navigate = useNavigate()
  const [form, setForm] = useState<ListingMutationPayload>(defaultForm)
  const [images, setImages] = useState<File[]>([])
  const [categories, setCategories] = useState<LookupItem[]>([])
  const [conditions, setConditions] = useState<LookupItem[]>([])
  const [residences, setResidences] = useState<LookupItem[]>([])
  const [pickupLocations, setPickupLocations] = useState<LookupItem[]>([])
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [highlightField, setHighlightField] = useState<ValidationField | null>(null)
  const cameraInputRef = useRef<HTMLInputElement | null>(null)
  const galleryInputRef = useRef<HTMLInputElement | null>(null)
  const titleFieldRef = useRef<HTMLDivElement | null>(null)
  const categoryFieldRef = useRef<HTMLDivElement | null>(null)
  const conditionFieldRef = useRef<HTMLDivElement | null>(null)
  const residenceFieldRef = useRef<HTMLDivElement | null>(null)
  const pickupLocationFieldRef = useRef<HTMLDivElement | null>(null)
  const priceFieldRef = useRef<HTMLDivElement | null>(null)
  const imagesFieldRef = useRef<HTMLDivElement | null>(null)
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

  useEffect(() => {
    let disposed = false
    void Promise.all([
      lookupApi.categories(),
      lookupApi.conditions(),
      lookupApi.residences(),
      lookupApi.pickupLocations(),
    ]).then(([c, cond, r, pick]) => {
      if (disposed) {
        return
      }
      setCategories(c)
      setConditions(cond)
      setResidences(r)
      setPickupLocations(pick)
    })
    return () => {
      disposed = true
    }
  }, [])

  const imagePreviews = useMemo(
    () =>
      images.map((file) => ({
        key: `${file.name}-${file.size}-${file.lastModified}`,
        url: URL.createObjectURL(file),
      })),
    [images],
  )

  useEffect(
    () => () => {
      imagePreviews.forEach((preview) => URL.revokeObjectURL(preview.url))
    },
    [imagePreviews],
  )

  const mergeSelectedImages = (incoming: FileList | null) => {
    if (!incoming?.length) {
      return
    }

    const nextFiles = Array.from(incoming)
    setImages((current) => {
      const merged = [...current]
      for (const file of nextFiles) {
        const duplicated = merged.some(
          (item) => item.name === file.name && item.size === file.size && item.lastModified === file.lastModified,
        )
        if (!duplicated) {
          merged.push(file)
        }
      }
      return merged
    })
  }

  const removeSelectedImage = (targetKey: string) => {
    setImages((current) =>
      current.filter((file) => `${file.name}-${file.size}-${file.lastModified}` !== targetKey),
    )
  }

  const validateForm = (): ValidationField[] => {
    const issues: ValidationField[] = []

    if (!form.title.trim()) {
      issues.push('title')
    }
    if (form.categoryCode <= 0) {
      issues.push('category')
    }
    if (form.conditionCode <= 0) {
      issues.push('condition')
    }
    if (form.residenceCode <= 0) {
      issues.push('residence')
    }
    if (form.pickupLocationCode <= 0) {
      issues.push('pickupLocation')
    }
    if (!form.isFree && (!Number.isFinite(form.price) || form.price < 0)) {
      issues.push('price')
    }
    if (images.length <= 0) {
      issues.push('images')
    }

    return issues
  }

  const getFieldContainer = (field: ValidationField): HTMLElement | null => {
    if (field === 'title') {
      return titleFieldRef.current
    }
    if (field === 'category') {
      return categoryFieldRef.current
    }
    if (field === 'condition') {
      return conditionFieldRef.current
    }
    if (field === 'residence') {
      return residenceFieldRef.current
    }
    if (field === 'pickupLocation') {
      return pickupLocationFieldRef.current
    }
    if (field === 'price') {
      return priceFieldRef.current
    }
    return imagesFieldRef.current
  }

  const focusInvalidField = (field: ValidationField) => {
    const container = getFieldContainer(field)
    if (!container) {
      return
    }

    setHighlightField(field)
    const scrollTargetY = Math.max(0, window.scrollY + container.getBoundingClientRect().top - 120)
    window.scrollTo({ top: scrollTargetY, behavior: 'smooth' })
    const focusTarget = container.querySelector<HTMLElement>('input, textarea, button, [tabindex]')
    window.setTimeout(() => {
      focusTarget?.focus({ preventScroll: true })
    }, 320)
  }

  const inputHighlightClass = (field: ValidationField) =>
    highlightField === field ? invalidBorderClass : ''

  const isSelectInvalid = (field: ValidationField) => highlightField === field

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const issues = validateForm()
    if (issues.length > 0) {
      setHighlightField(null)
      setError(null)
      focusInvalidField(issues[0])
      return
    }

    setSubmitting(true)
    setHighlightField(null)
    setError(null)
    try {
      const result = await listingApi.create(
        {
          ...form,
          price: form.isFree ? 0 : form.price,
        },
        images,
      )
      navigate(`/listings/${result.id}?from=create`)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '建立商品失敗')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className="mx-auto w-full max-w-4xl px-4 py-6 md:py-8">
      <section className="mb-8 space-y-3 text-center">
        <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="text-4xl font-semibold leading-tight text-text-main sm:text-5xl md:text-6xl">
          新增<span className="marker-wipe">刊登</span>
        </h1>
        <p className="mx-auto max-w-2xl text-xl text-text-subtle">填寫商品資訊並上傳圖片，讓社區住戶快速找到你。</p>
      </section>

      <Card>
        <form className="space-y-5" onSubmit={handleSubmit} noValidate>
          <div ref={titleFieldRef}>
            <Input
              label="標題"
              value={form.title}
              onChange={(event) => setForm((current) => ({ ...current, title: event.target.value }))}
              placeholder="例如：九成新電鍋"
              maxLength={80}
              className={`py-3 text-xl ${inputHighlightClass('title')}`}
              labelClassName="text-[1.45rem] font-bold text-text-main"
            />
          </div>
          <label className="flex flex-col gap-2 text-lg text-text-subtle">
            <span className="text-[1.45rem] font-bold leading-tight text-text-main">描述</span>
            <textarea
              className="min-h-32 w-full rounded-xl border border-border bg-surface px-3 py-3 text-xl text-text-main outline-none transition placeholder:text-text-muted focus:border-brand"
              value={form.description}
              onChange={(event) => setForm((current) => ({ ...current, description: event.target.value }))}
              placeholder="補充商品狀況、使用年限、注意事項..."
              maxLength={1000}
            />
          </label>

          <div className="grid gap-3 sm:grid-cols-2">
            <div ref={categoryFieldRef}>
              <ExpandableSelectField
                label="分類"
                value={form.categoryCode}
                options={categories}
                onChange={(value) => setForm((current) => ({ ...current, categoryCode: value }))}
                invalid={isSelectInvalid('category')}
              />
            </div>
            <div ref={conditionFieldRef}>
              <ExpandableSelectField
                label="品況"
                value={form.conditionCode}
                options={conditions}
                onChange={(value) => setForm((current) => ({ ...current, conditionCode: value }))}
                invalid={isSelectInvalid('condition')}
              />
            </div>
            <div ref={residenceFieldRef}>
              <ExpandableSelectField
                label="社宅"
                value={form.residenceCode}
                options={residences}
                onChange={(value) => setForm((current) => ({ ...current, residenceCode: value }))}
                invalid={isSelectInvalid('residence')}
              />
            </div>
            <div ref={pickupLocationFieldRef}>
              <ExpandableSelectField
                label="面交地點"
                value={form.pickupLocationCode}
                options={pickupLocations}
                onChange={(value) => setForm((current) => ({ ...current, pickupLocationCode: value }))}
                invalid={isSelectInvalid('pickupLocation')}
              />
            </div>
          </div>

          <div className="grid gap-3 sm:grid-cols-3">
            <button
              type="button"
              aria-pressed={form.isFree}
              className={toggleButtonClass('green', form.isFree)}
              onClick={() =>
                setForm((current) => ({ ...current, isFree: !current.isFree, price: !current.isFree ? 0 : current.price }))
              }
            >
              免費
            </button>
            <button
              type="button"
              aria-pressed={form.isCharity}
              className={toggleButtonClass('red', form.isCharity)}
              onClick={() => setForm((current) => ({ ...current, isCharity: !current.isCharity }))}
            >
              愛心捐贈
            </button>
            <button
              type="button"
              aria-pressed={form.isTradeable}
              className={toggleButtonClass('blue', form.isTradeable)}
              onClick={() => setForm((current) => ({ ...current, isTradeable: !current.isTradeable }))}
            >
              以物易物
            </button>
          </div>

          <div ref={priceFieldRef}>
            <Input
              label="價格（NT$）"
              type="number"
              value={form.price}
              min={0}
              disabled={form.isFree}
              className={`py-3 text-xl ${inputHighlightClass('price')}`}
              labelClassName="text-[1.45rem] font-bold text-text-main"
              onChange={(event) =>
                setForm((current) => ({ ...current, price: Number.isNaN(Number(event.target.value)) ? 0 : Number(event.target.value) }))
              }
            />
          </div>

          <div
            ref={imagesFieldRef}
            tabIndex={-1}
            className="flex flex-col gap-2 text-lg text-text-subtle"
          >
            <span className="text-[1.45rem] font-bold leading-tight text-text-main">商品照片（至少 1 張）</span>
            <input
              ref={cameraInputRef}
              type="file"
              accept="image/*"
              multiple
              capture="environment"
              className="hidden"
              onChange={(event) => mergeSelectedImages(event.target.files)}
            />
            <input
              ref={galleryInputRef}
              type="file"
              accept="image/*"
              multiple
              className="hidden"
              onChange={(event) => mergeSelectedImages(event.target.files)}
            />
            <div className="grid grid-cols-2 gap-2">
              <Button
                type="button"
                variant="secondary"
                className={`min-h-[3.2rem] text-[1.45rem] font-semibold text-[#4f463f] ${
                  highlightField === 'images' ? invalidBorderClass : ''
                }`}
                onClick={() => cameraInputRef.current?.click()}
              >
                拍照上傳
              </Button>
              <Button
                type="button"
                variant="secondary"
                className={`min-h-[3.2rem] text-[1.45rem] font-semibold text-[#4f463f] ${
                  highlightField === 'images' ? invalidBorderClass : ''
                }`}
                onClick={() => galleryInputRef.current?.click()}
              >
                從相簿選擇
              </Button>
            </div>
            {imagePreviews.length ? (
              <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
                {imagePreviews.map((preview) => (
                  <div key={preview.key} className="overflow-hidden rounded-xl border border-border bg-surface">
                    <img src={preview.url} alt="已選圖片預覽" className="aspect-square w-full object-cover" />
                    <div className="p-2">
                      <Button
                        type="button"
                        variant="secondary"
                        className="min-h-[2.6rem] w-full text-sm font-semibold"
                        onClick={() => removeSelectedImage(preview.key)}
                      >
                        移除
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <span className="text-base text-text-muted">尚未選擇圖片</span>
            )}
          </div>
          {error ? <p className="text-lg text-danger">{error}</p> : null}
          <Button type="submit" fullWidth className="min-h-[3.4rem] text-xl font-semibold" disabled={submitting}>
            {submitting ? '建立中...' : '建立商品'}
          </Button>
        </form>
      </Card>
    </main>
  )
}

