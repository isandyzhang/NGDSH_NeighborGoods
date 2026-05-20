import { useCallback, useEffect, useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { accountApi, type AccountMe, type LinePreferences, type StartLineBindingResponse } from '@/features/account/api/accountApi'
import { saveLineBindingPending } from '@/features/account/lineBindingSession'
import {
  appendLiffDebugToUrl,
  enableLineBindDebugSession,
  isLineBindDebugEnabled,
} from '@/features/account/lineNotifyLiffDiagnostics'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

const LINE_BINDING_COMPLETED_FLAG = 'neighborGoods.lineBindingCompleted'

export const AccountPage = () => {
  const navigate = useNavigate()
  const [searchParams, setSearchParams] = useSearchParams()
  const [profile, setProfile] = useState<AccountMe | null>(null)
  const [linePreferences, setLinePreferences] = useState<LinePreferences | null>(null)
  const [bindingStart, setBindingStart] = useState<StartLineBindingResponse | null>(null)
  const [lineContactDraft, setLineContactDraft] = useState('')
  const [loading, setLoading] = useState(true)
  const [actionLoading, setActionLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const reloadData = useCallback(async () => {
    const [me, prefs] = await Promise.all([accountApi.me(), accountApi.getLinePreferences()])
    setProfile(me)
    setLinePreferences(prefs)
  }, [])

  useEffect(() => {
    if (searchParams.get('lineBound') !== '1') {
      return
    }

    setSearchParams({}, { replace: true })
    void reloadData().catch(() => undefined)
  }, [reloadData, searchParams, setSearchParams])

  useEffect(() => {
    const refreshIfLineBindingCompleted = () => {
      const completed = sessionStorage.getItem(LINE_BINDING_COMPLETED_FLAG)
      if (completed !== '1') {
        return
      }

      sessionStorage.removeItem(LINE_BINDING_COMPLETED_FLAG)
      // 從 LINE 綁定頁關閉回來時，強制重抓帳號資料，確保 UI 立即反映綁定狀態。
      void reloadData().catch(() => undefined)
    }

    refreshIfLineBindingCompleted()
    window.addEventListener('focus', refreshIfLineBindingCompleted)
    document.addEventListener('visibilitychange', refreshIfLineBindingCompleted)
    return () => {
      window.removeEventListener('focus', refreshIfLineBindingCompleted)
      document.removeEventListener('visibilitychange', refreshIfLineBindingCompleted)
    }
  }, [reloadData])

  useEffect(() => {
    setLineContactDraft(profile?.lineContactId ?? '')
  }, [profile?.lineContactId])

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

  const openLineBindingWindow = useCallback(async (result: StartLineBindingResponse) => {
    if (isLineBindDebugEnabled(window.location.search)) {
      enableLineBindDebugSession()
    }
    const targetUrl = isLineBindDebugEnabled(window.location.search)
      ? appendLiffDebugToUrl(result.liffUrl)
      : result.liffUrl

    try {
      const liffMod = await import('@line/liff')
      if (liffMod.default.isInClient()) {
        // Full navigation — do not open liff.line.me inside the current LIFF webview (nested LIFF).
        window.location.assign(targetUrl)
        return
      }
    } catch {
      // fall through
    }
    window.open(targetUrl, '_blank', 'noopener,noreferrer')
  }, [])

  const handleStartLineOfficialBinding = async () => {
    if (!profile || actionLoading) {
      return
    }

    if (bindingStart) {
      await openLineBindingWindow(bindingStart)
      return
    }

    setActionLoading(true)
    setError(null)
    try {
      const result = await accountApi.startLineBinding()
      saveLineBindingPending(result.bindingToken, result.botLink)
      setBindingStart(result)
      await openLineBindingWindow(result)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '無法開始 LINE 綁定')
    } finally {
      setActionLoading(false)
    }
  }

  const handleLineOpenNotifications = async () => {
    if (actionLoading) {
      return
    }

    setActionLoading(true)
    setError(null)
    try {
      await enableLineNotify()
      await reloadData()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '開啟 LINE 通知失敗')
    } finally {
      setActionLoading(false)
    }
  }

  const handleLineCancelNotifications = async () => {
    if (actionLoading) {
      return
    }

    setActionLoading(true)
    setError(null)
    try {
      await accountApi.disableLineNotifications()
      await reloadData()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '取消 LINE 通知失敗')
    } finally {
      setActionLoading(false)
    }
  }

  const handleLineUnbind = async () => {
    if (actionLoading) {
      return
    }

    setActionLoading(true)
    setError(null)
    try {
      await accountApi.unbindLineBinding()
      await reloadData()
      setBindingStart(null)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '解除 LINE 綁定失敗')
    } finally {
      setActionLoading(false)
    }
  }

  const handleEmailOpenNotifications = async () => {
    if (actionLoading) {
      return
    }

    setActionLoading(true)
    setError(null)
    try {
      await accountApi.enableEmailNotifications()
      await reloadData()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '開啟 Email 通知失敗')
    } finally {
      setActionLoading(false)
    }
  }

  const handleEmailCancelNotifications = async () => {
    if (actionLoading) {
      return
    }

    setActionLoading(true)
    setError(null)
    try {
      await accountApi.disableEmailNotifications()
      await reloadData()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '取消 Email 通知失敗')
    } finally {
      setActionLoading(false)
    }
  }

  const handleSaveLineContact = async () => {
    if (actionLoading) {
      return
    }

    setActionLoading(true)
    setError(null)
    try {
      await accountApi.updateProfile({ lineContactId: lineContactDraft })
      await reloadData()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '儲存 LINE ID 失敗')
    } finally {
      setActionLoading(false)
    }
  }

  const emailNotifyStatusLine = useMemo(() => {
    if (!profile) {
      return ''
    }
    if (!profile.emailConfirmed) {
      return '尚未完成 Email 驗證，無法開啟通知'
    }
    if (!profile.emailNotificationEnabled) {
      return '目前狀態：通知已關閉'
    }
    return '目前狀態：通知已開啟'
  }, [profile])

  const lineNotifyStatusLine = useMemo(() => {
    if (!profile) {
      return ''
    }
    if (!profile.lineNotifyBound) {
      return '尚未綁定 LINE 官方通知帳號'
    }
    if (!lineNotifyEnabled) {
      return '目前狀態：通知已關閉'
    }
    return '目前狀態：通知已開啟'
  }, [profile, lineNotifyEnabled])

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

      {!loading && profile ? (
        <Card className="animate-fade-rise space-y-5" style={{ animationDelay: '120ms' }}>
          <div className="space-y-3">
            <p className="text-xs font-medium uppercase tracking-[0.14em] text-text-muted">通知開關</p>
            <div className="grid gap-3">
            <div className="rounded-xl border border-border bg-surface-2 px-3 py-3">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
                <div className="min-w-0 flex-1">
                  <p className="text-sm text-text-subtle">EMAIL 通知狀態</p>
                  <p
                    className={`mt-1 text-sm font-semibold ${
                      profile.emailConfirmed && profile.emailNotificationEnabled ? 'text-[#1E6B43]' : 'text-text-main'
                    }`}
                  >
                    {emailNotifyStatusLine}
                  </p>
                </div>
                <div className="flex shrink-0 justify-end sm:justify-end">
                {!profile.emailConfirmed ? (
                  <Button
                    type="button"
                    variant="secondary"
                    className="min-h-[2.6rem] px-4 text-sm font-semibold md:text-xs"
                    onClick={() => navigate('/account/email-verify')}
                  >
                    開始驗證
                  </Button>
                ) : !profile.emailNotificationEnabled ? (
                  <Button
                    type="button"
                    variant="secondary"
                    className="min-h-[2.6rem] px-4 text-sm font-semibold md:text-xs"
                    disabled={actionLoading}
                    onClick={() => void handleEmailOpenNotifications()}
                  >
                    {actionLoading ? '處理中...' : '打開EMAIL通知'}
                  </Button>
                ) : (
                  <Button
                    type="button"
                    variant="secondary"
                    className="text-sm font-semibold"
                    disabled={actionLoading}
                    onClick={() => void handleEmailCancelNotifications()}
                  >
                    {actionLoading ? '處理中...' : '取消通知'}
                  </Button>
                )}
                </div>
              </div>
            </div>

            <div className="rounded-xl border border-border bg-surface-2 px-3 py-3">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between sm:gap-4">
                <div className="min-w-0 flex-1">
                  <p className="text-sm text-text-subtle">LINE 通知狀態</p>
                  <p
                    className={`mt-1 text-sm font-semibold ${
                      profile.lineNotifyBound && lineNotifyEnabled ? 'text-[#1E6B43]' : 'text-text-main'
                    }`}
                  >
                    {lineNotifyStatusLine}
                  </p>
                </div>
                <div className="flex shrink-0 justify-end sm:justify-end">
                {!profile.lineNotifyBound ? (
                  <Button
                    type="button"
                    className="rounded-xl px-4 py-2 text-base font-semibold md:px-3 md:py-1.5 md:text-sm"
                    disabled={actionLoading}
                    onClick={() => void handleStartLineOfficialBinding()}
                  >
                    {actionLoading ? '準備中...' : '開始綁定'}
                  </Button>
                ) : !lineNotifyEnabled ? (
                  <Button
                    type="button"
                    className="rounded-xl px-4 py-2 text-base font-semibold md:px-3 md:py-1.5 md:text-sm"
                    disabled={actionLoading}
                    onClick={() => void handleLineOpenNotifications()}
                  >
                    {actionLoading ? '處理中...' : '打開LINE通知'}
                  </Button>
                ) : (
                  <div className="flex flex-wrap justify-end gap-2">
                    <Button
                      type="button"
                      className="rounded-xl px-4 py-2 text-base font-semibold md:px-3 md:py-1.5 md:text-sm"
                      disabled={actionLoading}
                      onClick={() => void handleLineCancelNotifications()}
                    >
                      {actionLoading ? '處理中...' : '取消通知'}
                    </Button>
                    <Button
                      type="button"
                      className="rounded-xl px-4 py-2 text-base font-semibold md:px-3 md:py-1.5 md:text-sm"
                      disabled={actionLoading}
                      onClick={() => void handleLineUnbind()}
                    >
                      {actionLoading ? '處理中...' : '解除綁定'}
                    </Button>
                  </div>
                )}
                </div>
              </div>
            </div>
            </div>
          </div>

          <div className="space-y-3 border-t border-border pt-4">
            <p className="text-xs font-medium uppercase tracking-[0.14em] text-text-muted">帳號與綁定</p>
            <div className="rounded-xl border border-border bg-surface-2 px-3 py-3">
              <p className="text-sm text-text-subtle">聊天分享用 LINE ID</p>
              <p className="mt-1 text-xs text-text-muted">僅允許英數與 . - _，留空可清除。</p>
              <div className="mt-3 flex flex-col gap-2 sm:flex-row">
                <input
                  className="min-h-[2.6rem] flex-1 rounded-xl border border-border bg-white px-3 py-2 text-sm outline-none focus:border-brand"
                  placeholder="例如 andy123"
                  value={lineContactDraft}
                  onChange={(event) => setLineContactDraft(event.target.value)}
                  maxLength={32}
                />
                <Button
                  type="button"
                  variant="secondary"
                  className="min-h-[2.6rem] px-4 text-sm font-semibold"
                  disabled={actionLoading}
                  onClick={() => void handleSaveLineContact()}
                >
                  {actionLoading ? '儲存中...' : '儲存 LINE ID'}
                </Button>
              </div>
            </div>
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
                <p
                  className={`text-lg font-semibold ${profile.emailConfirmed ? 'text-[#1E6B43]' : 'text-text-muted'}`}
                >
                  {profile.emailConfirmed ? '已驗證' : '未驗證'}
                </p>
              </div>
              <div>
                <p className="text-xs text-text-muted">LINE 綁定</p>
                <p
                  className={`text-lg font-semibold ${profile.lineNotifyBound ? 'text-[#1E6B43]' : 'text-text-muted'}`}
                >
                  {profile.lineNotifyBound ? '已綁定' : '未綁定'}
                </p>
              </div>
              <div>
                <p className="text-xs text-text-muted">註冊時間</p>
                <p className="text-lg font-semibold text-text-main">
                  {new Date(profile.createdAt).toLocaleDateString('zh-TW')}
                </p>
              </div>
            </div>
          </div>

          {!profile.lineNotifyBound ? (
            <p className="text-xs text-text-muted">
              點擊「開始綁定」後會直接開啟 LINE 綁定頁，並在有效期間內重用同一組綁定憑證。
              {isLineBindDebugEnabled(window.location.search) ? (
                <span className="mt-1 block text-amber-800">除錯模式已開啟（liffDebug=1）</span>
              ) : (
                <span className="mt-1 block">
                  除錯 LIFF 好友狀態：網址加上 <code className="text-[11px]">?liffDebug=1</code> 後再按開始綁定。
                </span>
              )}
            </p>
          ) : null}
        </Card>
      ) : null}
    </main>
  )
}
