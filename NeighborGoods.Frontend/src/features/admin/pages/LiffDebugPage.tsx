import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { accountApi } from '@/features/account/api/accountApi'
import { ADMIN_ROLE_CODE } from '@/features/admin/constants/adminRole'
import {
  buildLiffAdminDebugUrl,
  clearAdminLiffDebugSession,
  collectPostInitSnapshot,
  collectPreInitSnapshot,
  ENV_LIFF_ID,
  formatInitAttempt,
  formatPostInitSnapshot,
  formatPreInitSnapshot,
  HARDCODED_LIFF_ID,
  hasNestedLiffState,
  isAdminLiffDebugSessionActive,
  openLiffForAdminDebug,
  runLiffInitAttempt,
  type LiffInitAttemptResult,
  type LiffInitSource,
  type LiffPostInitSnapshot,
  type LiffPreInitSnapshot,
} from '@/features/admin/liffInitDebug'
import { useAuth } from '@/features/auth/components/AuthProvider'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'

type LiffDebugPageProps = {
  mode: 'admin' | 'liffEntry'
}

const copyToClipboard = async (text: string) => {
  try {
    await navigator.clipboard.writeText(text)
    return true
  } catch {
    return false
  }
}

export const LiffDebugPage = ({ mode }: LiffDebugPageProps) => {
  const location = useLocation()
  const { isAuthenticated } = useAuth()
  const [role, setRole] = useState<number | null>(null)
  const [roleLoading, setRoleLoading] = useState(mode === 'liffEntry')
  const [preSnapshot, setPreSnapshot] = useState<LiffPreInitSnapshot | null>(null)
  const [initAttempt, setInitAttempt] = useState<LiffInitAttemptResult | null>(null)
  const [postSnapshot, setPostSnapshot] = useState<LiffPostInitSnapshot | null>(null)
  const [initLoading, setInitLoading] = useState<LiffInitSource | null>(null)
  const [copyMessage, setCopyMessage] = useState<string | null>(null)

  const initPathOk = location.pathname === '/'
  const initEnabled = mode === 'liffEntry' && initPathOk

  const refreshPreSnapshot = useCallback(() => {
    setPreSnapshot(collectPreInitSnapshot())
  }, [])

  useEffect(() => {
    refreshPreSnapshot()
  }, [refreshPreSnapshot, location.pathname, location.search])

  useEffect(() => {
    if (mode !== 'liffEntry') {
      setRoleLoading(false)
      return
    }

    if (!isAuthenticated) {
      setRole(null)
      setRoleLoading(false)
      return
    }

    let disposed = false
    setRoleLoading(true)

    void accountApi
      .me()
      .then((profile) => {
        if (!disposed) {
          setRole(profile.role)
        }
      })
      .catch(() => {
        if (!disposed) {
          setRole(null)
        }
      })
      .finally(() => {
        if (!disposed) {
          setRoleLoading(false)
        }
      })

    return () => {
      disposed = true
    }
  }, [isAuthenticated, mode])

  const isAdmin = mode === 'admin' || role === ADMIN_ROLE_CODE
  const canInit = initEnabled && isAdmin && !roleLoading

  const envLiffId = ENV_LIFF_ID?.trim() ?? ''
  const envLiffUrl = useMemo(() => (envLiffId ? buildLiffAdminDebugUrl(envLiffId) : null), [envLiffId])
  const hardcodedLiffUrl = useMemo(() => buildLiffAdminDebugUrl(HARDCODED_LIFF_ID), [])

  const handleInit = useCallback(
    async (source: LiffInitSource) => {
      const liffId = source === 'env' ? ENV_LIFF_ID?.trim() : HARDCODED_LIFF_ID
      if (!liffId) {
        setInitAttempt({
          source,
          liffIdSuffix: '(none)',
          ok: false,
          durationMs: 0,
          errorName: 'ConfigError',
          errorCode: 'LIFF_ID_MISSING',
          errorMessage: 'VITE_LINE_LIFF_ID 未設定',
        })
        setPostSnapshot(null)
        return
      }

      setInitLoading(source)
      setInitAttempt(null)
      setPostSnapshot(null)
      refreshPreSnapshot()

      const result = await runLiffInitAttempt(liffId, source)
      setInitAttempt(result)
      console.info(formatInitAttempt(result))
      if (result.ok) {
        const post = await collectPostInitSnapshot()
        setPostSnapshot(post)
        console.info(formatPostInitSnapshot(post))
      }
      setInitLoading(null)
    },
    [refreshPreSnapshot],
  )

  const handleCopy = useCallback(async (text: string, label: string) => {
    const ok = await copyToClipboard(text)
    setCopyMessage(ok ? `已複製：${label}` : `複製失敗：${label}`)
    window.setTimeout(() => setCopyMessage(null), 2000)
  }, [])

  const handleEndDebug = useCallback(() => {
    clearAdminLiffDebugSession()
    if (mode === 'liffEntry') {
      window.location.replace('/listings')
      return
    }
    window.location.assign('/admin/liff-debug')
  }, [mode])

  const diagnosticText = [
    preSnapshot ? formatPreInitSnapshot(preSnapshot) : null,
    initAttempt ? formatInitAttempt(initAttempt) : null,
    postSnapshot ? formatPostInitSnapshot(postSnapshot) : null,
  ]
    .filter(Boolean)
    .join('\n\n')

  if (mode === 'liffEntry' && roleLoading) {
    return (
      <main className="mx-auto max-w-lg px-4 py-8">
        <p className="text-sm text-text-subtle">正在驗證管理員權限…</p>
      </main>
    )
  }

  if (mode === 'liffEntry' && !isAdmin) {
    return (
      <main className="mx-auto max-w-lg space-y-4 px-4 py-8">
        <h1 className="text-xl font-semibold text-text-main">LIFF init 除錯</h1>
        <p className="text-danger">
          僅管理員可使用。請先在電腦瀏覽器登入後台，按「在 LINE App 內開啟測試」再於 LINE 內操作。
        </p>
        <Link to="/login">
          <Button type="button" variant="secondary">
            前往登入
          </Button>
        </Link>
      </main>
    )
  }

  return (
    <main className="mx-auto max-w-lg space-y-4 px-4 py-8">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-semibold text-text-main">LIFF init 除錯</h1>
        {mode === 'admin' ? (
          <Link to="/admin" className="text-sm text-brand hover:underline">
            返回後台
          </Link>
        ) : null}
      </div>

      <Card className="border-amber-300 bg-amber-50">
        {mode === 'admin' ? (
          <p className="text-sm text-amber-900">
            請先按下方「在 LINE App 內開啟測試」，系統會記住除錯狀態並開啟 LIFF（網址<strong>不用</strong>手動加參數）。
            進入 LINE 後直接按 init 測試即可。
          </p>
        ) : (
          <p className="text-sm text-amber-900">
            你已在 LINE 內的除錯模式{isAdminLiffDebugSessionActive() ? '（session 已啟用）' : ''}。
            請直接按下方 init 按鈕測試。第二段診斷若出現 OAuth code，代表 LINE 登入回導完成，再按 init 即可。
          </p>
        )}
        {mode === 'liffEntry' && hasNestedLiffState(location.search) ? (
          <p className="mt-2 text-xs text-amber-800">
            偵測到 liff.state 重複嵌套，系統會自動整理網址；若 init 仍失敗請重新整理後再試。
          </p>
        ) : null}
        {mode === 'admin' ? (
          <p className="mt-2 text-xs text-amber-800">
            若你已在 LINE 內開啟此頁，請用上方按鈕（會直接導向根路徑，避免 liff.state 雙層嵌套）。
          </p>
        ) : null}
        {mode === 'admin' && !initPathOk ? (
          <p className="mt-2 text-xs text-amber-800">
            在電腦瀏覽器無法在此路徑執行 init；請用「在 LINE App 內開啟測試」。
          </p>
        ) : null}
      </Card>

      {mode === 'admin' ? (
        <Card className="space-y-3">
          <p className="text-sm font-semibold text-text-main">步驟 1：在 LINE 內開啟（一鍵）</p>
          <div className="flex flex-col gap-2">
            <Button
              type="button"
              className="w-full"
              disabled={!envLiffId}
              onClick={() => openLiffForAdminDebug(envLiffId)}
            >
              在 LINE App 內開啟測試（VITE_LINE_LIFF_ID）
            </Button>
            <Button type="button" variant="secondary" className="w-full" onClick={() => openLiffForAdminDebug(HARDCODED_LIFF_ID)}>
              在 LINE App 內開啟測試（硬編碼 prod）
            </Button>
          </div>
          {!envLiffId ? <p className="text-xs text-danger">VITE_LINE_LIFF_ID 未設定，env 按鈕無法使用。</p> : null}
          <details className="text-xs text-text-muted">
            <summary className="cursor-pointer">進階：複製 LIFF 連結</summary>
            <div className="mt-2 space-y-2">
              {envLiffUrl ? (
                <>
                  <p className="break-all">{envLiffUrl}</p>
                  <Button type="button" variant="secondary" className="w-full" onClick={() => void handleCopy(envLiffUrl, 'env LIFF URL')}>
                    複製 env 連結
                  </Button>
                </>
              ) : null}
              <p className="break-all">{hardcodedLiffUrl}</p>
              <Button
                type="button"
                variant="secondary"
                className="w-full"
                onClick={() => void handleCopy(hardcodedLiffUrl, 'hardcoded LIFF URL')}
              >
                複製 hardcoded 連結
              </Button>
            </div>
          </details>
          {copyMessage ? <p className="text-xs text-[#2F7D4E]">{copyMessage}</p> : null}
        </Card>
      ) : null}

      <Card className="space-y-3">
        <p className="text-sm font-semibold text-text-main">
          {mode === 'admin' ? '步驟 2：手動 liff.init()' : '手動 liff.init()'}
        </p>
        <p className="text-xs text-text-muted">若已 init 過，第二次可能失敗；請按「重新整理頁面」後再測。</p>
        <div className="flex flex-col gap-2">
          <Button type="button" className="w-full" disabled={!canInit || initLoading !== null} onClick={() => void handleInit('env')}>
            {initLoading === 'env' ? 'init 中…' : 'init（VITE_LINE_LIFF_ID）'}
          </Button>
          <Button
            type="button"
            variant="secondary"
            className="w-full"
            disabled={!canInit || initLoading !== null}
            onClick={() => void handleInit('hardcoded')}
          >
            {initLoading === 'hardcoded' ? 'init 中…' : 'init（硬編碼 prod）'}
          </Button>
          <Button type="button" variant="secondary" className="w-full" onClick={() => window.location.reload()}>
            重新整理頁面
          </Button>
          <Button type="button" variant="secondary" className="w-full" onClick={handleEndDebug}>
            結束除錯
          </Button>
        </div>
        {!canInit && isAdmin && mode === 'admin' ? (
          <p className="text-xs text-text-subtle">init 按鈕會在 LINE 內開啟後自動可用（根路徑 /）。</p>
        ) : null}
      </Card>

      {diagnosticText ? (
        <Card className="border-amber-200 bg-amber-50/80">
          <p className="mb-2 text-sm font-semibold text-amber-900">診斷輸出</p>
          <pre className="max-h-96 overflow-auto whitespace-pre-wrap break-all text-xs text-amber-950">{diagnosticText}</pre>
          <Button type="button" variant="secondary" className="mt-2 w-full" onClick={() => void handleCopy(diagnosticText, '診斷輸出')}>
            複製全部診斷
          </Button>
        </Card>
      ) : null}
    </main>
  )
}
