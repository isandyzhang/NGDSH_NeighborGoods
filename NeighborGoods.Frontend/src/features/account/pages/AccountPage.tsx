import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  accountApi,
  type AccountMe,
  type LineBindingStatusResponse,
  type LinePreferences,
  type StartLineBindingResponse,
} from '@/features/account/api/accountApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

export const AccountPage = () => {
  const [profile, setProfile] = useState<AccountMe | null>(null)
  const [linePreferences, setLinePreferences] = useState<LinePreferences | null>(null)
  const [bindingStart, setBindingStart] = useState<StartLineBindingResponse | null>(null)
  const [bindingStatus, setBindingStatus] = useState<LineBindingStatusResponse | null>(null)
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
    let disposed = false
    setError(null)
    setLoading(true)

    void reloadData()
      .then(() => {
        if (!disposed) {
          setBindingStart(null)
          setBindingStatus(null)
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
        setBindingStatus({ status: 'waiting', message: '請加入官方帳號後，回來按「檢查綁定狀態」。' })
        window.open(result.botLink, '_blank', 'noopener,noreferrer')
      }
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '處理 LINE 通知設定失敗')
    } finally {
      setActionLoading(false)
    }
  }

  const handleCheckBindingStatus = async () => {
    if (!bindingStart || actionLoading) {
      return
    }

    setActionLoading(true)
    setError(null)
    try {
      const status = await accountApi.getLineBindingStatus(bindingStart.pendingBindingId)
      setBindingStatus(status)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '檢查 LINE 綁定狀態失敗')
    } finally {
      setActionLoading(false)
    }
  }

  const handleConfirmBinding = async () => {
    if (!bindingStart || actionLoading) {
      return
    }

    setActionLoading(true)
    setError(null)
    try {
      await accountApi.confirmLineBinding(bindingStart.pendingBindingId)
      await reloadData()
      await enableLineNotify()
      setBindingStatus({ status: 'completed', message: 'LINE 官方通知綁定成功。' })
      setSuccessText('LINE 官方通知已綁定並啟用')
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '確認 LINE 綁定失敗')
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
              <p className="text-base font-semibold text-text-main">LINE 綁定流程</p>
              <p className="mt-1 text-sm text-text-subtle">1) 先加入官方帳號 2) 回來檢查狀態 3) 顯示 ready 後按確認綁定</p>
              <div className="mt-3 grid gap-3 sm:grid-cols-[auto_1fr]">
                <img src={bindingStart.qrCodeUrl} alt="LINE 綁定 QRCode" className="h-32 w-32 rounded-lg border border-border bg-white p-1" />
                <div className="space-y-2">
                  <a
                    href={bindingStart.botLink}
                    target="_blank"
                    rel="noreferrer"
                    className="inline-flex rounded-xl border border-border bg-surface px-4 py-2 text-sm font-semibold text-text-main transition hover:bg-surface-2"
                  >
                    開啟 LINE 官方帳號
                  </a>
                  <div className="flex flex-wrap gap-2">
                    <Button type="button" variant="secondary" className="text-sm" disabled={actionLoading} onClick={() => void handleCheckBindingStatus()}>
                      檢查綁定狀態
                    </Button>
                    <Button
                      type="button"
                      className="text-sm"
                      disabled={actionLoading || bindingStatus?.status !== 'ready'}
                      onClick={() => void handleConfirmBinding()}
                    >
                      確認綁定
                    </Button>
                  </div>
                  <p className="text-sm text-text-subtle">{bindingStatus?.message ?? '等待你完成官方帳號加入'}</p>
                </div>
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
