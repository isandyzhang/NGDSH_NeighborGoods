import { useEffect, useMemo, useState } from 'react'
import { surveyApi, type SubmitSurveyPayload, type SurveyGiftStatus } from '@/features/survey/api/surveyApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { Input } from '@/shared/ui/Input'

type RatingValue = SubmitSurveyPayload['ratingUi']
type ChannelValue = SubmitSurveyPayload['channel']

const ratingOptions: Array<{ value: RatingValue; label: string }> = [
  { value: 'satisfied', label: '滿意' },
  { value: 'fair', label: '還好' },
  { value: 'unsatisfied', label: '不滿意' },
]

const ratingQuestions: Array<{ key: keyof Pick<SubmitSurveyPayload, 'ratingUi' | 'ratingListing' | 'ratingInteraction' | 'ratingGuide' | 'ratingPromo'>; label: string }> = [
  { key: 'ratingUi', label: '介面與瀏覽' },
  { key: 'ratingListing', label: '刊登與管理' },
  { key: 'ratingInteraction', label: '互動與媒合' },
  { key: 'ratingGuide', label: '說明清楚度' },
  { key: 'ratingPromo', label: '宣傳管道' },
]

const channelOptions: Array<{ value: ChannelValue; label: string }> = [
  { value: 'friends', label: '親友／鄰居／管委' },
  { value: 'bulletin', label: '公佈欄／海報／大廳 TV' },
  { value: 'youth_online', label: '青創線上（FB・LINE）' },
  { value: 'other', label: '其他' },
]

const labelClassName = 'text-[1.45rem] font-bold text-text-main'
const choiceButtonBase =
  'rounded-xl border px-3 py-2 text-base transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand'

export const SurveyPage = () => {
  const [residentType, setResidentType] = useState<'social' | 'non_social' | ''>('')
  const [displayName, setDisplayName] = useState('')
  const [phone, setPhone] = useState('')
  const [building, setBuilding] = useState<'south' | 'north' | ''>('')
  const [floor, setFloor] = useState('')
  const [ratings, setRatings] = useState<Record<string, RatingValue | ''>>({
    ratingUi: '',
    ratingListing: '',
    ratingInteraction: '',
    ratingGuide: '',
    ratingPromo: '',
  })
  const [channel, setChannel] = useState<ChannelValue | ''>('')
  const [feedback, setFeedback] = useState('')
  const [consentAccepted, setConsentAccepted] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [successText, setSuccessText] = useState<string | null>(null)
  const [giftStatus, setGiftStatus] = useState<SurveyGiftStatus | null>(null)
  const [statusLoading, setStatusLoading] = useState(true)

  useEffect(() => {
    let disposed = false
    setStatusLoading(true)
    void surveyApi
      .getGiftStatus()
      .then((status) => {
        if (!disposed) {
          setGiftStatus(status)
          if (status.alreadySubmitted) {
            setSuccessText(
              status.alreadyGiftEligible
                ? `你已填寫過問卷，可領取${status.giftName}（每人限領一瓶）。${status.pickupNote}`
                : '你已填寫過問卷（每人限填一次）。贈品名額當時可能已額滿，仍謝謝你的回饋。',
            )
          }
        }
      })
      .catch((err) => {
        if (!disposed) {
          setGiftStatus(null)
          setError(err instanceof ApiClientError ? err.message : '無法載入贈品狀態')
        }
      })
      .finally(() => {
        if (!disposed) {
          setStatusLoading(false)
        }
      })
    return () => {
      disposed = true
    }
  }, [])

  const alreadySubmitted = giftStatus?.alreadySubmitted === true

  const canSubmit = useMemo(() => {
    if (alreadySubmitted || !residentType || !channel || !consentAccepted) {
      return false
    }

    if (ratingQuestions.some((q) => !ratings[q.key])) {
      return false
    }

    if (residentType === 'social') {
      const floorNumber = Number(floor)
      if (!building || !Number.isInteger(floorNumber) || floorNumber < 1 || floorNumber > 50) {
        return false
      }
    }

    return true
  }, [alreadySubmitted, building, channel, consentAccepted, floor, ratings, residentType])

  const resetForm = () => {
    setResidentType('')
    setDisplayName('')
    setPhone('')
    setBuilding('')
    setFloor('')
    setRatings({
      ratingUi: '',
      ratingListing: '',
      ratingInteraction: '',
      ratingGuide: '',
      ratingPromo: '',
    })
    setChannel('')
    setFeedback('')
    setConsentAccepted(false)
  }

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError(null)
    setSuccessText(null)

    if (!canSubmit || !residentType || !channel) {
      setError('請完成必填欄位')
      return
    }

    setSubmitting(true)
    try {
      const result = await surveyApi.submit({
        residentType,
        displayName: displayName.trim() || null,
        phone: phone.trim() || null,
        building: residentType === 'social' ? (building as 'south' | 'north') : null,
        floor: residentType === 'social' ? Number(floor) : null,
        ratingUi: ratings.ratingUi as RatingValue,
        ratingListing: ratings.ratingListing as RatingValue,
        ratingInteraction: ratings.ratingInteraction as RatingValue,
        ratingGuide: ratings.ratingGuide as RatingValue,
        ratingPromo: ratings.ratingPromo as RatingValue,
        channel,
        feedback: feedback.trim() || null,
        consentAccepted: true,
      })
      resetForm()
      setGiftStatus((current) =>
        current
          ? {
              ...current,
              remainingGiftSlots: result.remainingGiftSlots,
              isAvailable: result.remainingGiftSlots > 0,
              alreadySubmitted: true,
              alreadyGiftEligible: result.isGiftEligible,
            }
          : current,
      )
      setSuccessText(
        result.isGiftEligible
          ? `感謝填寫！你是前 ${giftStatus?.giftLimit ?? 24} 名，可領取${giftStatus?.giftName ?? '一瓶御茶園麥萃日式麥茶'}（每人限領一瓶）。贈品將放在用餐區，請自行取用。`
          : '感謝填寫！問卷已送出。贈品名額已額滿，仍謝謝你的回饋。',
      )
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '送出失敗，請稍後再試')
      if (err instanceof ApiClientError && err.message.includes('已填寫')) {
        setGiftStatus((current) =>
          current
            ? {
                ...current,
                alreadySubmitted: true,
              }
            : current,
        )
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className="mx-auto w-full max-w-4xl px-4 py-6 md:py-8">
      <section className="mb-8 space-y-3 text-center">
        <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="text-4xl font-semibold leading-tight text-text-main sm:text-5xl md:text-6xl">
          填寫<span className="marker-wipe">問卷</span>
        </h1>
        <p className="text-xl text-text-subtle">需登入後填寫；每人限填一次、限領一瓶。</p>
      </section>

      {giftStatus ? (
        <div className="mb-5 rounded-xl border border-border bg-surface-2 px-4 py-4 text-left text-base text-text-subtle md:text-lg">
          <p className="font-semibold text-text-main">
            前 {giftStatus.giftLimit} 名填寫可領取：{giftStatus.giftName}
          </p>
          <p className="mt-1">
            {giftStatus.isAvailable
              ? `目前還可領取 ${giftStatus.remainingGiftSlots} 瓶（數量有限，每人限領一瓶）。`
              : '贈品名額已額滿，仍歡迎填寫問卷。'}
          </p>
          <p className="mt-1">{giftStatus.pickupNote}</p>
          {giftStatus.giftListingPath ? (
            <p className="mt-2">
              也可到站內商品頁領取：
              <a href={giftStatus.giftListingPath} className="ml-1 font-medium text-text-main underline">
                前往贈品刊登
              </a>
            </p>
          ) : null}
        </div>
      ) : null}

      <Card>
        {statusLoading ? (
          <p className="text-base text-text-subtle">載入中…</p>
        ) : (
          <form className="space-y-6" onSubmit={(event) => void handleSubmit(event)}>
            <fieldset className="space-y-3" disabled={alreadySubmitted}>
              <legend className={labelClassName}>身份（必填）</legend>
              <div className="flex flex-wrap gap-2">
                {(
                  [
                    { value: 'social', label: '社宅住戶' },
                    { value: 'non_social', label: '非社宅住戶' },
                  ] as const
                ).map((option) => (
                  <button
                    key={option.value}
                    type="button"
                    disabled={alreadySubmitted}
                    className={`${choiceButtonBase} ${
                      residentType === option.value
                        ? 'border-brand bg-brand/10 font-semibold text-text-main'
                        : 'border-border bg-surface text-text-subtle'
                    }`}
                    onClick={() => {
                      setResidentType(option.value)
                      if (option.value !== 'social') {
                        setBuilding('')
                        setFloor('')
                      }
                    }}
                  >
                    {option.label}
                  </button>
                ))}
              </div>
            </fieldset>

            <Input
              label="姓名（選填）"
              value={displayName}
              onChange={(event) => setDisplayName(event.target.value)}
              placeholder="怎麼稱呼你"
              className="py-3 text-xl"
              labelClassName={labelClassName}
              maxLength={100}
              disabled={alreadySubmitted}
            />

            <Input
              label="手機（選填）"
              value={phone}
              onChange={(event) => setPhone(event.target.value)}
              placeholder="09xxxxxxxx"
              className="py-3 text-xl"
              labelClassName={labelClassName}
              maxLength={32}
              disabled={alreadySubmitted}
            />

            {residentType === 'social' ? (
              <fieldset className="space-y-3" disabled={alreadySubmitted}>
                <legend className={labelClassName}>住址（社宅）</legend>
                <div className="flex flex-wrap gap-2">
                  {(
                    [
                      { value: 'south', label: '南棟' },
                      { value: 'north', label: '北棟' },
                    ] as const
                  ).map((option) => (
                    <button
                      key={option.value}
                      type="button"
                      disabled={alreadySubmitted}
                      className={`${choiceButtonBase} ${
                        building === option.value
                          ? 'border-brand bg-brand/10 font-semibold text-text-main'
                          : 'border-border bg-surface text-text-subtle'
                      }`}
                      onClick={() => setBuilding(option.value)}
                    >
                      {option.label}
                    </button>
                  ))}
                </div>
                <Input
                  label="樓層"
                  type="number"
                  value={floor}
                  onChange={(event) => setFloor(event.target.value)}
                  placeholder="例如 12"
                  className="py-3 text-xl"
                  labelClassName="text-lg font-semibold text-text-main"
                  min={1}
                  max={50}
                  disabled={alreadySubmitted}
                />
              </fieldset>
            ) : null}

            <fieldset className="space-y-4" disabled={alreadySubmitted}>
              <legend className={labelClassName}>滿意度（必填）</legend>
              {ratingQuestions.map((question) => (
                <div key={question.key} className="space-y-2">
                  <p className="text-lg font-semibold text-text-main">{question.label}</p>
                  <div className="flex flex-wrap gap-2">
                    {ratingOptions.map((option) => (
                      <button
                        key={option.value}
                        type="button"
                        disabled={alreadySubmitted}
                        className={`${choiceButtonBase} ${
                          ratings[question.key] === option.value
                            ? 'border-brand bg-brand/10 font-semibold text-text-main'
                            : 'border-border bg-surface text-text-subtle'
                        }`}
                        onClick={() => setRatings((current) => ({ ...current, [question.key]: option.value }))}
                      >
                        {option.label}
                      </button>
                    ))}
                  </div>
                </div>
              ))}
            </fieldset>

            <fieldset className="space-y-3" disabled={alreadySubmitted}>
              <legend className={labelClassName}>得知管道（必填）</legend>
              <div className="flex flex-col gap-2 sm:flex-row sm:flex-wrap">
                {channelOptions.map((option) => (
                  <button
                    key={option.value}
                    type="button"
                    disabled={alreadySubmitted}
                    className={`${choiceButtonBase} text-left ${
                      channel === option.value
                        ? 'border-brand bg-brand/10 font-semibold text-text-main'
                        : 'border-border bg-surface text-text-subtle'
                    }`}
                    onClick={() => setChannel(option.value)}
                  >
                    {option.label}
                  </button>
                ))}
              </div>
            </fieldset>

            <label className="flex flex-col gap-2 text-lg text-text-subtle">
              <span className={labelClassName}>建議／Feedback（選填）</span>
              <textarea
                value={feedback}
                onChange={(event) => setFeedback(event.target.value)}
                className="min-h-28 w-full rounded-xl border border-border bg-surface px-3 py-3 text-xl text-text-main outline-none transition focus:border-brand disabled:opacity-60"
                placeholder="有什麼想跟我們說的…"
                maxLength={2000}
                disabled={alreadySubmitted}
              />
            </label>

            <label className="flex items-start gap-3 text-base text-text-subtle">
              <input
                type="checkbox"
                checked={consentAccepted}
                onChange={(event) => setConsentAccepted(event.target.checked)}
                className="mt-1 h-5 w-5 accent-[var(--color-brand,theme(colors.brand))]"
                disabled={alreadySubmitted}
              />
              <span>我同意提供上述資料，供臺北市青創計畫活動統計使用（必勾）</span>
            </label>

            {error ? <p className="text-base text-danger">{error}</p> : null}
            {successText ? <p className="text-base text-[#2F7D4E]">{successText}</p> : null}

            <Button type="submit" fullWidth disabled={!canSubmit || submitting || alreadySubmitted} className="min-h-[3.4rem] text-xl font-semibold">
              {alreadySubmitted ? '已填寫過問卷' : submitting ? '送出中…' : '送出問卷'}
            </Button>
          </form>
        )}
      </Card>
    </main>
  )
}
