import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { accountApi } from '@/features/account/api/accountApi'
import { listingApi, type ListingMutationPayload } from '@/features/listings/api/listingApi'
import { TOP_PIN_FOCUS_QUERY } from '@/features/listings/constants/topPin'
import { lookupApi, type LookupItem } from '@/features/lookups/api/lookupApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { ExpandableSelectField } from '@/shared/ui/ExpandableSelectField'
import { Input } from '@/shared/ui/Input'

export const EditListingPage = () => {
  const navigate = useNavigate()
  const { id = '' } = useParams()
  const [searchParams] = useSearchParams()
  const [form, setForm] = useState<ListingMutationPayload | null>(null)
  const [categories, setCategories] = useState<LookupItem[]>([])
  const [conditions, setConditions] = useState<LookupItem[]>([])
  const [residences, setResidences] = useState<LookupItem[]>([])
  const [pickupLocations, setPickupLocations] = useState<LookupItem[]>([])
  const [existingImageUrls, setExistingImageUrls] = useState<string[]>([])
  const [imageUrlsToDelete, setImageUrlsToDelete] = useState<string[]>([])
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [uploadingImages, setUploadingImages] = useState(false)
  const [topPinCredits, setTopPinCredits] = useState<number>(0)
  const [isPinned, setIsPinned] = useState(false)
  const [pinnedEndDate, setPinnedEndDate] = useState<string | null>(null)
  const [topPinBusy, setTopPinBusy] = useState(false)
  const [topPinHighlighted, setTopPinHighlighted] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const cameraInputRef = useRef<HTMLInputElement | null>(null)
  const galleryInputRef = useRef<HTMLInputElement | null>(null)
  const topPinSectionRef = useRef<HTMLElement | null>(null)
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
    if (!id) {
      return
    }

    let disposed = false
    setLoading(true)

    void Promise.all([
      listingApi.getById(id),
      lookupApi.categories(),
      lookupApi.conditions(),
      lookupApi.residences(),
      lookupApi.pickupLocations(),
      accountApi.me(),
    ])
      .then(([detail, c, cond, r, pick, me]) => {
        if (disposed) {
          return
        }
        setCategories(c)
        setConditions(cond)
        setResidences(r)
        setPickupLocations(pick)
        setForm({
          title: detail.title,
          description: detail.description ?? '',
          categoryCode: detail.categoryCode,
          conditionCode: detail.conditionCode,
          price: detail.price,
          residenceCode: detail.residenceCode,
          pickupLocationCode: detail.pickupLocationCode,
          isFree: detail.isFree,
          isCharity: detail.isCharity,
          isTradeable: detail.isTradeable,
        })
        setExistingImageUrls(detail.imageUrls)
        setImageUrlsToDelete([])
        setTopPinCredits(me.statistics.topPinCredits)
        setIsPinned(detail.isPinned)
        setPinnedEndDate(detail.pinnedEndDate)
      })
      .catch((err: unknown) => {
        if (!disposed) {
          setError(err instanceof ApiClientError ? err.message : '讀取商品資料失敗')
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
  }, [id])

  useEffect(() => {
    const shouldFocusTopPin = searchParams.get('focus') === TOP_PIN_FOCUS_QUERY
    if (!shouldFocusTopPin || !form) {
      return
    }

    const timer = window.setTimeout(() => {
      topPinSectionRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
      setTopPinHighlighted(true)
    }, 120)

    return () => {
      window.clearTimeout(timer)
    }
  }, [form, searchParams])

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!form || !id) {
      return
    }

    setSubmitting(true)
    setError(null)
    try {
      const imageUrlsInOrder = existingImageUrls.filter((url) => !imageUrlsToDelete.includes(url))
      await listingApi.update(id, {
        ...form,
        price: form.isFree ? 0 : form.price,
      }, imageUrlsToDelete, imageUrlsInOrder)
      navigate(`/listings/${id}?from=edit`)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '更新商品失敗')
    } finally {
      setSubmitting(false)
    }
  }

  const uploadSelectedImages = async (incoming: FileList | null) => {
    if (!id || !incoming?.length || uploadingImages) {
      return
    }

    const files = Array.from(incoming)
    setUploadingImages(true)
    setError(null)
    try {
      for (const file of files) {
        await listingApi.addImage(id, file)
      }
      const refreshed = await listingApi.getById(id)
      setExistingImageUrls(refreshed.imageUrls)
      setImageUrlsToDelete((current) => current.filter((url) => refreshed.imageUrls.includes(url)))
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '上傳圖片失敗')
    } finally {
      setUploadingImages(false)
    }
  }

  const handleUseTopPin = async () => {
    if (!id || topPinBusy) {
      return
    }

    setTopPinBusy(true)
    setError(null)
    try {
      await listingApi.topPin(id)
      const [detail, me] = await Promise.all([listingApi.getById(id), accountApi.me()])
      setIsPinned(detail.isPinned)
      setPinnedEndDate(detail.pinnedEndDate)
      setTopPinCredits(me.statistics.topPinCredits)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '置頂操作失敗')
    } finally {
      setTopPinBusy(false)
    }
  }

  if (loading) {
    return (
      <main className="mx-auto w-full max-w-4xl px-4 py-6 md:py-8">
        <Card className="h-64 animate-pulse bg-surface-2" />
      </main>
    )
  }

  return (
    <main className="mx-auto w-full max-w-4xl px-4 py-6 md:py-8">
      <div className="mb-4">
        <Link to="/my-listings" className="text-base text-text-subtle hover:text-text-main">
          ← 返回我的商品
        </Link>
      </div>
      <section className="mb-8 space-y-3 text-center">
        <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="text-4xl font-semibold leading-tight text-text-main sm:text-5xl md:text-6xl">
          編輯<span className="marker-wipe">商品</span>
        </h1>
        <p className="mx-auto max-w-2xl text-xl text-text-subtle">更新你的刊登內容，儲存後會立即生效。</p>
      </section>
      <Card>
        {form ? (
          <form className="space-y-5" onSubmit={handleSubmit}>
            <Input
              label="標題"
              value={form.title}
              onChange={(event) => setForm((current) => (current ? { ...current, title: event.target.value } : current))}
              maxLength={80}
              className="py-3 text-xl"
              labelClassName="text-[1.45rem] font-bold text-text-main"
              required
            />
            <label className="flex flex-col gap-2 text-lg text-text-subtle">
              <span className="text-[1.45rem] font-bold leading-tight text-text-main">描述</span>
              <textarea
                className="min-h-32 w-full rounded-xl border border-border bg-surface px-3 py-3 text-xl text-text-main outline-none transition placeholder:text-text-muted focus:border-brand"
                value={form.description}
                onChange={(event) =>
                  setForm((current) => (current ? { ...current, description: event.target.value } : current))
                }
                maxLength={1000}
              />
            </label>
            <div className="grid gap-3 sm:grid-cols-2">
              <ExpandableSelectField
                label="分類"
                value={form.categoryCode}
                options={categories}
                onChange={(next) => setForm((current) => (current ? { ...current, categoryCode: next } : current))}
              />
              <ExpandableSelectField
                label="品況"
                value={form.conditionCode}
                options={conditions}
                onChange={(next) => setForm((current) => (current ? { ...current, conditionCode: next } : current))}
              />
              <ExpandableSelectField
                label="社宅"
                value={form.residenceCode}
                options={residences}
                onChange={(next) => setForm((current) => (current ? { ...current, residenceCode: next } : current))}
              />
              <ExpandableSelectField
                label="面交地點"
                value={form.pickupLocationCode}
                options={pickupLocations}
                onChange={(next) =>
                  setForm((current) => (current ? { ...current, pickupLocationCode: next } : current))
                }
              />
            </div>

            <div className="grid gap-3 sm:grid-cols-3">
              <button
                type="button"
                aria-pressed={form.isFree}
                className={toggleButtonClass('green', form.isFree)}
                onClick={() =>
                  setForm((current) =>
                    current ? { ...current, isFree: !current.isFree, price: !current.isFree ? 0 : current.price } : current,
                  )
                }
              >
                免費
              </button>
              <button
                type="button"
                aria-pressed={form.isCharity}
                className={toggleButtonClass('red', form.isCharity)}
                onClick={() => setForm((current) => (current ? { ...current, isCharity: !current.isCharity } : current))}
              >
                愛心捐贈
              </button>
              <button
                type="button"
                aria-pressed={form.isTradeable}
                className={toggleButtonClass('blue', form.isTradeable)}
                onClick={() => setForm((current) => (current ? { ...current, isTradeable: !current.isTradeable } : current))}
              >
                以物易物
              </button>
            </div>

            <Input
              label="價格（NT$）"
              type="number"
              value={form.price}
              min={0}
              disabled={form.isFree}
              className="py-3 text-xl"
              labelClassName="text-[1.45rem] font-bold text-text-main"
              onChange={(event) =>
                setForm((current) =>
                  current ? { ...current, price: Number.isNaN(Number(event.target.value)) ? 0 : Number(event.target.value) } : current,
                )
              }
              required={!form.isFree}
            />

            <div className="space-y-3">
              <p className="text-[1.45rem] font-bold leading-tight text-text-main">商品圖片</p>
              {existingImageUrls.length ? (
                <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
                  {existingImageUrls.map((url, index) => {
                    const selected = imageUrlsToDelete.includes(url)
                    return (
                      <div
                        key={url}
                        className="overflow-hidden rounded-xl border border-border bg-surface transition"
                      >
                        <img src={url} alt="商品圖片" className="aspect-square w-full object-cover" />
                        <div className="space-y-2 p-2">
                          <p className="text-center text-xs text-text-muted">第 {index + 1} 張</p>
                          <Button
                            type="button"
                            variant="secondary"
                            className={`min-h-[2.6rem] w-full text-sm font-semibold ${
                              selected
                                ? '!border-[#D48A8A] !bg-[#FDE2E2] !text-[#B23A3A] hover:!bg-[#F8D1D1]'
                                : ''
                            }`}
                            onClick={() =>
                              setImageUrlsToDelete((current) =>
                                selected ? current.filter((item) => item !== url) : [...current, url],
                              )
                            }
                          >
                            {selected ? '已標記刪除（再按取消）' : '刪除此圖'}
                          </Button>
                        </div>
                      </div>
                    )
                  })}
                </div>
              ) : (
                <p className="text-base text-text-muted">目前沒有可顯示的圖片。</p>
              )}
            </div>

            <section
              id="top-pin"
              ref={topPinSectionRef}
              className={`space-y-3 rounded-2xl border p-4 transition ${
                topPinHighlighted ? 'border-[#B08F68] bg-[#F5EBDD]' : 'border-border bg-surface'
              }`}
            >
              <div className="space-y-1">
                <h2 className="text-[1.45rem] font-bold leading-tight text-text-main">新增置頂功能區</h2>
                <p className="text-base text-text-subtle">每次使用 1 次置頂，商品可置頂 7 天，讓曝光更穩定。</p>
                <p className="text-sm text-text-muted">可用置頂次數：{topPinCredits}</p>
                {isPinned ? (
                  <p className="text-sm font-semibold text-[#8A6B45]">
                    目前狀態：置頂中{pinnedEndDate ? `（到期：${new Date(pinnedEndDate).toLocaleString()}）` : ''}
                  </p>
                ) : (
                  <p className="text-sm text-text-muted">目前狀態：尚未置頂</p>
                )}
              </div>
              <Button
                type="button"
                onClick={() => void handleUseTopPin()}
                disabled={topPinBusy || isPinned}
                className="min-h-[3rem] px-5 font-semibold"
              >
                {isPinned ? '目前已置頂' : topPinBusy ? '處理中...' : '使用 1 次置頂（7 天）'}
              </Button>
            </section>

            <div className="space-y-2">
              <div className="flex flex-col gap-2 text-lg text-text-subtle">
                <span className="text-[1.45rem] font-bold leading-tight text-text-main">新增圖片</span>
                <input
                  ref={cameraInputRef}
                  type="file"
                  accept="image/*"
                  multiple
                  capture="environment"
                  className="hidden"
                  onChange={(event) => {
                    void uploadSelectedImages(event.target.files)
                    event.currentTarget.value = ''
                  }}
                />
                <input
                  ref={galleryInputRef}
                  type="file"
                  accept="image/*"
                  multiple
                  className="hidden"
                  onChange={(event) => {
                    void uploadSelectedImages(event.target.files)
                    event.currentTarget.value = ''
                  }}
                />
                <div className="grid grid-cols-2 gap-2">
                  <Button
                    type="button"
                    variant="secondary"
                    className="min-h-[3.2rem] text-[1.45rem] font-semibold text-[#4f463f]"
                    disabled={uploadingImages}
                    onClick={() => cameraInputRef.current?.click()}
                  >
                    {uploadingImages ? '上傳中...' : '拍照上傳'}
                  </Button>
                  <Button
                    type="button"
                    variant="secondary"
                    className="min-h-[3.2rem] text-[1.45rem] font-semibold text-[#4f463f]"
                    disabled={uploadingImages}
                    onClick={() => galleryInputRef.current?.click()}
                  >
                    從相簿選擇
                  </Button>
                </div>
              </div>
              <p className="text-base text-text-muted">選擇圖片後會立即上傳並更新到現有圖片。</p>
            </div>
            {error ? <p className="text-lg text-danger">{error}</p> : null}
            <Button type="submit" fullWidth className="min-h-[3.4rem] text-xl font-semibold" disabled={submitting}>
              {submitting ? '儲存中...' : '儲存變更'}
            </Button>
          </form>
        ) : (
          <p className="text-sm text-text-muted">找不到可編輯的商品資料。</p>
        )}
      </Card>
    </main>
  )
}

