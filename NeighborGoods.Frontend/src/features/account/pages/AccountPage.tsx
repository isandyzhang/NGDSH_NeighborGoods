import { useCallback, useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { accountApi, type AccountMe, type LinePreferences, type StartLineBindingResponse } from '@/features/account/api/accountApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

export const AccountPage = () => {
  const [searchParams, setSearchParams] = useSearchParams()
  const [profile, setProfile] = useState<AccountMe | null>(null)
  const [linePreferences, setLinePreferences] = useState<LinePreferences | null>(null)
  const [bindingStart, setBindingStart] = useState<StartLineBindingResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [actionLoading, setActionLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [successText, setSuccessText] = useState<string | null>(null)

  const reloadData = useCallback(async () => {
    const [me, prefs] = await Promise.all([accountApi.me(), accountApi.getLinePreferences()])
    setProfile(me)
    setLinePreferences(prefs)
  }, [])

  useEffect(() => {
    if (searchParams.get('lineBound') !== '1') {
      return
    }

    setSuccessText('LINE 官方通知已綁定。若尚未啟用推播，請於下方啟用。')
    setSearchParams({}, { replace: true })
    void reloadData().catch(() => undefined)
  }, [reloadData, searchParams, setSearchParams])

  useEffect(() => {
    let disposed = false
    setError(null)
    setLoading(true)

    void reloadData()
      .then(() => {
        if (!disposed) {
          setBindingStart(null)
        }
      })
      .catch((err: unknown) => {
        if (disposed) {
          return
        }
        setError(err instanceof ApiClientError ? err.message : '讀取我的帳號失敗')
      })
      .finally(() => {
        if (!disposed) {
          setLoading(false)
        }
      })

    return () => {
      disposed = true
    }
  }, [reloadData])

  const lineNotifyEnabled = useMemo(() => Boolean(linePreferences?.marketingPushEnabled), [linePreferences?.marketingPushEnabled])

  const enableLineNotify = useCallback(async () => {
    const current = linePreferences
    if (!current) {
      return
    }

    const hasAnyDetail = current.preferenceMessageDigest || current.preferenceNewListings || current.preferencePriceDrop
    const updated = await accountApi.updateLinePreferences({
      marketingPushEnabled: true,
      preferenceMessageDigest: hasAnyDetail ? current.preferenceMessageDigest : true,
      preferenceNewListings: current.preferenceNewListings,
      preferencePriceDrop: current.preferencePriceDrop,
    })
    setLinePreferences(updated)
  }, [linePreferences])

  const handleLineMainAction = async () => {
    if (!profile || actionLoading) {
      return
    }

    setActionLoading(true)
    setError(null)
    setSuccessText(null)
    try {
      if (profile.lineNotifyBound) {
        await enableLineNotify()
        setSuccessText('已啟用 LINE 官方通知')
      } else {
        const result = await accountApi.startLineBinding()
        setBindingStart(result)
        try {
          const liffMod = await import('@line/liff')
          const liff = liffMod.default
          const liffId = import.meta.env.VITE_LINE_LIFF_ID as string | undefined
          if (liffId?.trim() && liff.isInClient()) {
            await liff.init({ liffId: liffId.trim() })
            await liff.openWindow({ url: result.liffUrl, external: false })
          } else {
            window.open(result.liffUrl, '_blank', 'noopener,noreferrer')
          }
        } catch {
          window.open(result.liffUrl, '_blank', 'noopener,noreferrer')
        }
      }
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '處理 LINE 通知設定失敗')
    } finally {
      setActionLoading(false)
    }
  }

  return (
    <main className="mx-auto w-full max-w-4xl px-4 py-6 md:py-8">
      <section className="animate-fade-rise mb-8 space-y-3 text-center">
        <p className="animate-fade-in text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="text-5xl font-semibold leading-tight text-text-main sm:text-6xl md:text-7xl">
          我的<span className="marker-wipe">帳號</span>
        </h1>
      </section>

      {loading ? <PageSkeleton className="h-52" /> : null}
      {error ? <ErrorState description={error} /> : null}
      {successText ? <p className="mb-4 text-base text-[#2F7D4E]">{successText}</p> : null}

      {!loading && profile ? (
        <Card className="animate-fade-rise space-y-4" style={{ animationDelay: '120ms' }}>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <p className="text-xs text-text-muted">顯示名稱</p>
              <p className="text-lg font-semibold text-text-main">{profile.displayName}</p>
            </div>
            <div>
              <p className="text-xs text-text-muted">帳號</p>
              <p className="break-all text-lg font-semibold text-text-main">{profile.userName}</p>
            </div>
            <div>
              <p className="text-xs text-text-muted">Email</p>
              <p className="text-lg font-semibold text-text-main">{profile.email ?? '未設定'}</p>
            </div>
            <div>
              <p className="text-xs text-text-muted">Email 驗證</p>
              <p className={`text-lg font-semibold ${profile.emailConfirmed ? 'text-[#1E6B43]' : 'text-danger'}`}>
                {profile.emailConfirmed ? '已驗證' : 'EMAIL未驗證'}
              </p>
            </div>
            <div>
              <p className="text-xs text-text-muted">LINE 綁定</p>
              <p className={`text-lg font-semibold ${profile.lineUserId ? 'text-[#1E6B43]' : 'text-text-muted'}`}>
                {profile.lineUserId ? '已綁定' : '未綁定'}
              </p>
            </div>
            <div>
              <p className="text-xs text-text-muted">註冊時間</p>
              <p className="text-lg font-semibold text-text-main">
                {new Date(profile.createdAt).toLocaleDateString('zh-TW')}
              </p>
            </div>
          </div>

          <div className="grid gap-3 border-t border-border pt-4">
            <StatusRow
              label="Email 驗證狀態"
              ok={profile.emailConfirmed}
              okText="Email 已驗證"
              emptyText="EMAIL未驗證"
            />
            <StatusRow
              label="LINE 官方通知"
              ok={profile.lineNotifyBound && lineNotifyEnabled}
              okText="LINE 官方通知已啟用"
              emptyText={profile.lineNotifyBound ? '已綁定官方帳號，但通知尚未啟用' : 'LINE未綁定官方通知帳號'}
            />
          </div>

          <Button
            type="button"
            fullWidth
            variant={profile.lineNotifyBound && lineNotifyEnabled ? 'secondary' : 'primary'}
            className="min-h-[3rem] text-base font-semibold"
            disabled={actionLoading || (profile.lineNotifyBound && lineNotifyEnabled)}
            onClick={() => void handleLineMainAction()}
          >
            {profile.lineNotifyBound
              ? lineNotifyEnabled
                ? 'LINE 官方通知已啟用'
                : actionLoading
                  ? '啟用中...'
                  : '啟用 LINE 官方通知'
              : actionLoading
                ? '準備綁定中...'
                : '綁定 LINE 官方通知帳號'}
          </Button>

          {!profile.lineNotifyBound && bindingStart ? (
            <Card className="border-dashed border-[#D8C0A3] bg-[#FFF9F1] p-4">
              <p className="text-base font-semibold text-text-main">LINE 綁定（LIFF）</p>
              <p className="mt-1 text-sm text-text-subtle">
                請在已開啟的 LINE 畫面依序完成加好友（若需要）與「完成綁定」。若未自動開啟，請點下方連結。
              </p>
              <div className="mt-3 space-y-2">
                <a
                  href={bindingStart.liffUrl}
                  target="_blank"
                  rel="noreferrer"
                  className="inline-flex rounded-xl border border-border bg-surface px-4 py-2 text-sm font-semibold text-text-main transition hover:bg-surface-2"
                >
                  開啟 LINE 綁定畫面
                </a>
                <p className="text-xs text-text-muted">綁定完成後回到此頁重新整理，或依畫面提示啟用通知。</p>
              </div>
            </Card>
          ) : null}
        </Card>
      ) : null}
    </main>
  )
}

const StatusRow = ({
  label,
  ok,
  okText,
  emptyText,
}: {
  label: string
  ok: boolean
  okText: string
  emptyText: string
}) => (
  <div className="flex items-center justify-between rounded-xl border border-border bg-surface-2 px-3 py-2">
    <span className="text-sm text-text-subtle">{label}</span>
    <span className={`inline-flex items-center gap-2 text-sm font-semibold ${ok ? 'text-[#1E6B43]' : 'text-text-muted'}`}>
      <span className={`h-3 w-3 rounded-full border ${ok ? 'border-[#1E6B43] bg-[#1E6B43]' : 'border-border bg-transparent'}`} />
      {ok ? okText : emptyText}
    </span>
  </div>
)
