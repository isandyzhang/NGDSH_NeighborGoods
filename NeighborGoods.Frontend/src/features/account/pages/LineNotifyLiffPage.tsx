import liff from '@line/liff'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { buildLineNotifyBindingLoginRedirectUri } from '@/app/liffRoute'
import {
  clearLineBindingPending,
  resolveLineBindingParams,
  saveLineBindingPending,
} from '@/features/account/lineBindingSession'
import {
  collectLineNotifyLiffDiagnostics,
  formatLineNotifyLiffDiagnosticsLog,
  isLineBindDebugEnabled,
  type LineNotifyLiffDiagnostics,
} from '@/features/account/lineNotifyLiffDiagnostics'
import { env } from '@/shared/config/env'
import { unwrapApiResponse, type ApiResponse } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'

type Phase = 'loading' | 'debug' | 'needLineApp' | 'submitting' | 'done' | 'error'

const liffId = import.meta.env.VITE_LINE_LIFF_ID as string | undefined
const LINE_BINDING_COMPLETED_FLAG = 'neighborGoods.lineBindingCompleted'
const DEBUG_MIN_PAUSE_MS = 4000

const LineNotifyLiffDebugPanel = ({
  diagnostics,
  onRefresh,
  onContinue,
  refreshing,
}: {
  diagnostics: LineNotifyLiffDiagnostics
  onRefresh: () => void
  onContinue: () => void
  refreshing: boolean
}) => (
  <div className="space-y-3 rounded-lg border border-amber-300 bg-amber-50 p-3 text-left">
    <p className="text-sm font-semibold text-amber-900">LIFF 綁定除錯（liffDebug=1）</p>
    <pre className="max-h-64 overflow-auto whitespace-pre-wrap break-all text-xs text-amber-950">
      {formatLineNotifyLiffDiagnosticsLog(diagnostics)}
    </pre>
    <p className="text-xs text-amber-800">
      加好友由 LINE Console「Add friend option」處理；綁定只依 idToken + bindToken 寫入 DB。friendFlag 僅供參考。
    </p>
    <div className="flex flex-col gap-2">
      <Button type="button" variant="secondary" className="w-full" disabled={refreshing} onClick={onRefresh}>
        {refreshing ? '檢測中…' : '重新整理狀態'}
      </Button>
      <Button type="button" className="w-full" onClick={onContinue}>
        完成綁定（寫入 DB）
      </Button>
    </div>
  </div>
)

export const LineNotifyLiffPage = () => {
  const [searchParams] = useSearchParams()
  const search = searchParams.toString()
  const liffDebug = useMemo(
    () => isLineBindDebugEnabled(search ? `?${search}` : window.location.search),
    [search],
  )
  const { bindToken, botLink } = useMemo(
    () => resolveLineBindingParams(search ? `?${search}` : window.location.search),
    [search],
  )
  const loginRedirectUri = useMemo(() => buildLineNotifyBindingLoginRedirectUri(), [])

  const [phase, setPhase] = useState<Phase>('loading')
  const [errorText, setErrorText] = useState<string | null>(null)
  const [diagnostics, setDiagnostics] = useState<LineNotifyLiffDiagnostics | null>(null)
  const [debugRefreshing, setDebugRefreshing] = useState(false)

  const logDiagnostics = useCallback((d: LineNotifyLiffDiagnostics) => {
    const text = formatLineNotifyLiffDiagnosticsLog(d)
    console.info(text)
    console.info('[LIFF bind debug JSON]', d)
  }, [])

  const postComplete = useCallback(async () => {
    const idToken = liff.getIDToken()
    if (!idToken) {
      saveLineBindingPending(bindToken, botLink)
      liff.login({ redirectUri: loginRedirectUri })
      return
    }

    const res = await fetch(`${env.apiBaseUrl}/api/v1/account/line/bind/liff-complete`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ bindingToken: bindToken, idToken }),
    })
    const json = (await res.json()) as ApiResponse<{ bound: boolean }>
    unwrapApiResponse(json)
  }, [bindToken, botLink, loginRedirectUri])

  const finishAndClose = useCallback(async () => {
    setPhase('done')
    clearLineBindingPending()
    sessionStorage.setItem(LINE_BINDING_COMPLETED_FLAG, '1')
    const accountUrl = `${window.location.origin}/account?lineBound=1`
    if (liff.isInClient()) {
      try {
        await liff.closeWindow()
        return
      } catch {
        // fall through to redirect
      }
    }

    window.location.replace(accountUrl)
  }, [])

  const runComplete = useCallback(async () => {
    setPhase('submitting')
    setErrorText(null)
    try {
      await postComplete()
      await finishAndClose()
    } catch (err) {
      setPhase('error')
      setErrorText(err instanceof Error ? err.message : '綁定失敗')
    }
  }, [finishAndClose, postComplete])

  const showDebugPause = useCallback(
    async (d: LineNotifyLiffDiagnostics) => {
      setPhase('debug')
      setDiagnostics(d)
      logDiagnostics(d)
      await new Promise((resolve) => window.setTimeout(resolve, DEBUG_MIN_PAUSE_MS))
    },
    [logDiagnostics],
  )

  const handleDebugRefresh = useCallback(async () => {
    setDebugRefreshing(true)
    try {
      const d = await collectLineNotifyLiffDiagnostics(liffId, bindToken, botLink)
      setDiagnostics(d)
      logDiagnostics(d)
      setPhase('debug')
    } finally {
      setDebugRefreshing(false)
    }
  }, [bindToken, botLink, logDiagnostics])

  useEffect(() => {
    let disposed = false

    void (async () => {
      if (!liffId?.trim()) {
        setPhase('error')
        setErrorText('VITE_LINE_LIFF_ID 未設定，無法完成 LIFF 綁定。')
        return
      }

      if (!bindToken) {
        setPhase('error')
        setErrorText('缺少綁定參數，請至網站「我的帳號」重新開始綁定。')
        return
      }

      try {
        await liff.init({ liffId: liffId.trim() })
        if (disposed) {
          return
        }

        if (!liff.isInClient()) {
          setPhase('needLineApp')
          return
        }

        if (!liff.isLoggedIn()) {
          saveLineBindingPending(bindToken, botLink)
          liff.login({ redirectUri: loginRedirectUri })
          return
        }

        if (liffDebug) {
          const d = await collectLineNotifyLiffDiagnostics(liffId, bindToken, botLink)
          if (disposed) {
            return
          }
          await showDebugPause(d)
          return
        }

        await runComplete()
      } catch (err) {
        if (!disposed) {
          setPhase('error')
          setErrorText(err instanceof Error ? err.message : 'LIFF 初始化失敗')
        }
      }
    })()

    return () => {
      disposed = true
    }
  }, [bindToken, botLink, liffDebug, loginRedirectUri, runComplete, showDebugPause])

  return (
    <main className="mx-auto flex min-h-[50vh] max-w-md flex-col justify-center gap-4 px-4 py-8">
      <h1 className="text-xl font-semibold text-text-main">LINE 官方通知綁定</h1>

      {liffDebug ? (
        <p className="text-xs text-amber-800">除錯模式：請查看下方狀態與 Console（[LIFF bind debug]）</p>
      ) : (
        <p className="text-xs text-text-muted">
          加好友請依 LINE 提示完成；此頁僅完成帳號綁定。
        </p>
      )}

      {phase === 'loading' ? <p className="text-text-subtle">載入中…</p> : null}

      {phase === 'debug' && diagnostics ? (
        <LineNotifyLiffDebugPanel
          diagnostics={diagnostics}
          refreshing={debugRefreshing}
          onRefresh={() => void handleDebugRefresh()}
          onContinue={() => void runComplete()}
        />
      ) : null}

      {phase === 'needLineApp' ? (
        <p className="text-text-subtle">請在 LINE App 內開啟此連結，以完成綁定。</p>
      ) : null}

      {phase === 'submitting' ? <p className="text-text-subtle">綁定中…</p> : null}

      {phase === 'done' ? <p className="text-[#2F7D4E]">綁定成功，您可以關閉此畫面。</p> : null}

      {phase === 'error' && errorText ? (
        <div className="space-y-3">
          <p className="text-danger">{errorText}</p>
          {liffDebug && diagnostics ? (
            <pre className="max-h-32 overflow-auto rounded border border-amber-200 bg-amber-50 p-2 text-xs">
              {formatLineNotifyLiffDiagnosticsLog(diagnostics)}
            </pre>
          ) : null}
          <Button
            type="button"
            variant="secondary"
            className="w-full"
            onClick={() => {
              setErrorText(null)
              void runComplete()
            }}
          >
            重試完成綁定
          </Button>
        </div>
      ) : null}
    </main>
  )
}
