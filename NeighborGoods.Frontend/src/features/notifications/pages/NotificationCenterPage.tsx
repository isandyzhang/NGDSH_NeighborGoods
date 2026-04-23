import { useEffect, useMemo, useState } from 'react'
import { accountApi, type LinePreferences, type LineQuotaStatus } from '@/features/account/api/accountApi'
import { listingApi, type InterestProfile } from '@/features/listings/api/listingApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

type EditablePreferences = Omit<LinePreferences, 'lastPreferencePushSentAt'>

const defaultPrefs: EditablePreferences = {
  marketingPushEnabled: false,
  preferenceNewListings: false,
  preferencePriceDrop: false,
  preferenceMessageDigest: false,
}

export const NotificationCenterPage = () => {
  const [preferences, setPreferences] = useState<LinePreferences | null>(null)
  const [draftPrefs, setDraftPrefs] = useState<EditablePreferences>(defaultPrefs)
  const [quota, setQuota] = useState<LineQuotaStatus | null>(null)
  const [interestProfile, setInterestProfile] = useState<InterestProfile | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [successText, setSuccessText] = useState<string | null>(null)

  useEffect(() => {
    let disposed = false
    setLoading(true)
    setError(null)

    void Promise.all([accountApi.getLinePreferences(), accountApi.getLineQuota(), listingApi.getInterestProfile(90, 5)])
      .then(([prefs, quotaResult, interest]) => {
        if (disposed) {
          return
        }
        setPreferences(prefs)
        setDraftPrefs({
          marketingPushEnabled: prefs.marketingPushEnabled,
          preferenceNewListings: prefs.preferenceNewListings,
          preferencePriceDrop: prefs.preferencePriceDrop,
          preferenceMessageDigest: prefs.preferenceMessageDigest,
        })
        setQuota(quotaResult)
        setInterestProfile(interest)
      })
      .catch((err: unknown) => {
        if (disposed) {
          return
        }
        setError(err instanceof ApiClientError ? err.message : '讀取通知中心資料失敗')
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

  const hasChanges = useMemo(
    () =>
      preferences
        ? preferences.marketingPushEnabled !== draftPrefs.marketingPushEnabled ||
          preferences.preferenceNewListings !== draftPrefs.preferenceNewListings ||
          preferences.preferencePriceDrop !== draftPrefs.preferencePriceDrop ||
          preferences.preferenceMessageDigest !== draftPrefs.preferenceMessageDigest
        : false,
    [draftPrefs, preferences],
  )

  const handleSave = async () => {
    if (!hasChanges || saving) {
      return
    }

    setSaving(true)
    setError(null)
    setSuccessText(null)
    try {
      const updated = await accountApi.updateLinePreferences(draftPrefs)
      setPreferences(updated)
      setDraftPrefs({
        marketingPushEnabled: updated.marketingPushEnabled,
        preferenceNewListings: updated.preferenceNewListings,
        preferencePriceDrop: updated.preferencePriceDrop,
        preferenceMessageDigest: updated.preferenceMessageDigest,
      })
      setSuccessText('通知偏好已更新')
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '更新通知偏好失敗')
    } finally {
      setSaving(false)
    }
  }

  return (
    <main className="mx-auto w-full max-w-5xl px-4 py-6 md:py-8">
      <section className="mb-8 space-y-3 text-center">
        <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="text-4xl font-semibold leading-tight text-text-main sm:text-5xl md:text-6xl">
          通知<span className="marker-wipe">中心</span>
        </h1>
        <p className="text-lg text-text-subtle">管理 LINE 推播偏好、查看本月配額與你的興趣分類。</p>
      </section>

      {loading ? <PageSkeleton className="h-64" /> : null}
      {error ? <ErrorState description={error} /> : null}
      {successText ? <p className="mb-4 text-base text-[#2F7D4E]">{successText}</p> : null}

      {!loading ? (
        <div className="grid gap-4 lg:grid-cols-2">
          <Card className="space-y-4">
            <h2 className="text-2xl font-semibold text-text-main">LINE 通知偏好</h2>
            <ToggleRow
              label="開啟行銷推播"
              value={draftPrefs.marketingPushEnabled}
              onChange={(next) =>
                setDraftPrefs((current) => ({
                  ...current,
                  marketingPushEnabled: next,
                  preferenceNewListings: next ? current.preferenceNewListings : false,
                  preferencePriceDrop: next ? current.preferencePriceDrop : false,
                  preferenceMessageDigest: next ? current.preferenceMessageDigest : false,
                }))
              }
            />
            <ToggleRow
              label="新上架通知"
              value={draftPrefs.preferenceNewListings}
              disabled={!draftPrefs.marketingPushEnabled}
              onChange={(next) => setDraftPrefs((current) => ({ ...current, preferenceNewListings: next }))}
            />
            <ToggleRow
              label="價格異動通知"
              value={draftPrefs.preferencePriceDrop}
              disabled={!draftPrefs.marketingPushEnabled}
              onChange={(next) => setDraftPrefs((current) => ({ ...current, preferencePriceDrop: next }))}
            />
            <ToggleRow
              label="訊息摘要通知"
              value={draftPrefs.preferenceMessageDigest}
              disabled={!draftPrefs.marketingPushEnabled}
              onChange={(next) => setDraftPrefs((current) => ({ ...current, preferenceMessageDigest: next }))}
            />
            <p className="text-sm text-text-muted">
              上次偏好推送時間：
              {preferences?.lastPreferencePushSentAt
                ? new Date(preferences.lastPreferencePushSentAt).toLocaleString('zh-TW')
                : '尚無紀錄'}
            </p>
            <Button
              type="button"
              className="min-h-[3rem] w-full text-base font-semibold"
              disabled={!hasChanges || saving}
              onClick={() => void handleSave()}
            >
              {saving ? '儲存中...' : '儲存偏好設定'}
            </Button>
          </Card>

          <Card className="space-y-4">
            <h2 className="text-2xl font-semibold text-text-main">推播配額狀態</h2>
            {quota ? (
              <>
                <p className="text-base text-text-subtle">{quota.note}</p>
                <div className="grid grid-cols-2 gap-3 text-base">
                  <QuotaItem label="本月已使用" value={String(quota.usedCount)} />
                  <QuotaItem label="本月剩餘" value={quota.remainingCount == null ? '不限' : String(quota.remainingCount)} />
                  <QuotaItem label="月配額" value={quota.monthlyQuota == null ? '不限' : String(quota.monthlyQuota)} />
                  <QuotaItem label="使用率" value={quota.usagePercent == null ? '—' : `${quota.usagePercent}%`} />
                </div>
              </>
            ) : (
              <EmptyState title="暫無配額資料" description="請稍後再試或檢查後端 LINE 設定。" />
            )}
          </Card>
        </div>
      ) : null}

      {!loading ? (
        <Card className="mt-4">
          <h2 className="mb-3 text-2xl font-semibold text-text-main">興趣分類（近 90 天）</h2>
          {interestProfile?.topCategories.length ? (
            <div className="flex flex-wrap gap-2">
              {interestProfile.topCategories.map((category) => (
                <span key={category.categoryCode} className="rounded-full border border-[#D8C0A3] bg-[#F8EFE4] px-3 py-1 text-sm text-text-main">
                  {category.categoryName}（{category.favoriteCount}）
                </span>
              ))}
            </div>
          ) : (
            <EmptyState title="尚無偏好資料" description="先收藏幾件商品後，這裡會顯示你的常看分類。" />
          )}
        </Card>
      ) : null}
    </main>
  )
}

const ToggleRow = ({
  label,
  value,
  disabled = false,
  onChange,
}: {
  label: string
  value: boolean
  disabled?: boolean
  onChange: (next: boolean) => void
}) => (
  <label className="flex items-center justify-between gap-3 rounded-xl border border-border bg-surface-2 px-3 py-3">
    <span className={`text-base ${disabled ? 'text-text-muted' : 'text-text-main'}`}>{label}</span>
    <button
      type="button"
      className={`h-8 min-w-[4.2rem] rounded-full border px-2 text-sm font-semibold transition ${
        value ? 'border-transparent bg-[#2F7D4E] text-white' : 'border-border bg-surface text-text-subtle'
      } ${disabled ? 'cursor-not-allowed opacity-50' : ''}`}
      disabled={disabled}
      onClick={() => onChange(!value)}
      aria-pressed={value}
    >
      {value ? '開啟' : '關閉'}
    </button>
  </label>
)

const QuotaItem = ({ label, value }: { label: string; value: string }) => (
  <div className="rounded-xl border border-border bg-surface-2 px-3 py-2">
    <p className="text-sm text-text-muted">{label}</p>
    <p className="text-lg font-semibold text-text-main">{value}</p>
  </div>
)
