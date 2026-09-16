import { useEffect, useMemo, useRef, useState } from 'react'
import { listingApi, type ListingCreateFormState, type ListingMutationPayload } from '@/features/listings/api/listingApi'
import {
  CreateListingSuccessModal,
  type CreatedListingSummary,
} from '@/features/listings/components/CreateListingSuccessModal'
import { FirstListingSellerWelcomeModal } from '@/features/listings/components/FirstListingSellerWelcomeModal'
import { ListingFormFields, type ListingFormHighlightField } from '@/features/listings/components/ListingFormFields'
import { hasSeenFirstListingSellerWelcome } from '@/features/listings/constants/firstListingSellerWelcome'
import { LISTING_IMAGE_MAX_COUNT, LISTING_IMAGE_MAX_FILE_SIZE_BYTES } from '@/features/listings/constants/listingLimits'
import { lookupApi, type LookupItem } from '@/features/lookups/api/lookupApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'

const invalidBorderClass = '!border-2 !border-[#dc2626] transition-colors duration-300 ease-out'

const defaultForm: ListingCreateFormState = {
  title: '',
  description: '',
  categoryCode: null,
  conditionCode: null,
  price: 0,
  residenceCode: null,
  pickupLocationCode: null,
  isFree: false,
  isCharity: false,
  isTradeable: false,
}

type ValidationField = ListingFormHighlightField | 'images'

const resolveLookupName = (items: LookupItem[], code: number | null) =>
  code === null ? '' : (items.find((item) => item.id === code)?.displayName ?? '')

const toMutationPayload = (form: ListingCreateFormState): ListingMutationPayload | null => {
  if (
    form.categoryCode === null ||
    form.conditionCode === null ||
    form.residenceCode === null ||
    form.pickupLocationCode === null
  ) {
    return null
  }

  return {
    ...form,
    categoryCode: form.categoryCode,
    conditionCode: form.conditionCode,
    residenceCode: form.residenceCode,
    pickupLocationCode: form.pickupLocationCode,
  }
}

export const CreateListingPage = () => {
  const [form, setForm] = useState<ListingCreateFormState>(defaultForm)
  const [images, setImages] = useState<File[]>([])
  const [categories, setCategories] = useState<LookupItem[]>([])
  const [conditions, setConditions] = useState<LookupItem[]>([])
  const [residences, setResidences] = useState<LookupItem[]>([])
  const [pickupLocations, setPickupLocations] = useState<LookupItem[]>([])
  const [submitting, setSubmitting] = useState(false)
  const [createdListing, setCreatedListing] = useState<CreatedListingSummary | null>(null)
  const [successModalOpen, setSuccessModalOpen] = useState(false)
  const [sellerWelcomeOpen, setSellerWelcomeOpen] = useState(false)
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
    }).catch(() => {
      if (!disposed) {
        setError('載入選項失敗，請稍後再試')
      }
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
    if (form.categoryCode === null) {
      issues.push('category')
    }
    if (form.conditionCode === null) {
      issues.push('condition')
    }
    if (form.residenceCode === null) {
      issues.push('residence')
    }
    if (form.pickupLocationCode === null) {
      issues.push('pickupLocation')
    }
    if (!form.isFree && (!Number.isFinite(form.price) || form.price < 0)) {
      issues.push('price')
    }
    if (images.length <= 0 || images.length > LISTING_IMAGE_MAX_COUNT) {
      issues.push('images')
    }
    if (images.some((file) => file.size > LISTING_IMAGE_MAX_FILE_SIZE_BYTES)) {
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
      const mutationPayload = toMutationPayload(form)
      if (!mutationPayload) {
        return
      }

      const payload = {
        ...mutationPayload,
        price: form.isFree ? 0 : form.price,
      }
      const mineBeforeCreate = await listingApi.listMine(1, 1)
      const isFirstListing = mineBeforeCreate.pagination.totalCount === 0
      const result = await listingApi.create(payload, images)
      setCreatedListing({
        id: result.id,
        title: payload.title.trim(),
        categoryName: resolveLookupName(categories, payload.categoryCode),
        conditionName: resolveLookupName(conditions, payload.conditionCode),
        isFree: payload.isFree,
        price: payload.price,
      })
      if (isFirstListing && !hasSeenFirstListingSellerWelcome()) {
        setSellerWelcomeOpen(true)
      } else {
        setSuccessModalOpen(true)
      }
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
          <ListingFormFields
            form={form}
            lookups={{ categories, conditions, residences, pickupLocations }}
            onChange={(patch) => setForm((current) => ({ ...current, ...patch }))}
            highlightField={highlightField === 'images' ? null : highlightField}
            fieldRefs={{
              title: titleFieldRef,
              category: categoryFieldRef,
              condition: conditionFieldRef,
              residence: residenceFieldRef,
              pickupLocation: pickupLocationFieldRef,
              price: priceFieldRef,
            }}
            includeEmptyOptions
          />

          <div
            ref={imagesFieldRef}
            tabIndex={-1}
            className="flex flex-col gap-2 text-lg text-text-subtle"
          >
            <span className="text-[1.45rem] font-bold leading-tight text-text-main">
              商品照片（至少 1 張，最多 {LISTING_IMAGE_MAX_COUNT} 張）
            </span>
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

      <FirstListingSellerWelcomeModal
        open={sellerWelcomeOpen}
        onAcknowledge={() => {
          setSellerWelcomeOpen(false)
          setSuccessModalOpen(true)
        }}
        onClose={() => setSellerWelcomeOpen(false)}
      />

      <CreateListingSuccessModal
        open={successModalOpen}
        listing={createdListing}
        onClose={() => setSuccessModalOpen(false)}
      />
    </main>
  )
}

