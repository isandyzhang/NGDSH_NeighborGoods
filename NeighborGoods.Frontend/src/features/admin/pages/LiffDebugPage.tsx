import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { accountApi } from '@/features/account/api/accountApi'
import { ADMIN_ROLE_CODE } from '@/features/admin/constants/adminRole'
import {
  buildLiffAdminDebugUrl,
  collectPostInitSnapshot,
  collectPreInitSnapshot,
  ENV_LIFF_ID,
  formatInitAttempt,
  formatPostInitSnapshot,
  formatPreInitSnapshot,
  HARDCODED_LIFF_ID,
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

  const envLiffUrl = useMemo(() => {
    const id = ENV_LIFF_ID?.trim()
    return id ? buildLiffAdminDebugUrl(id) : null
  }, [])

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
        <p className="text-danger">僅管理員可使用此除錯入口。請先登入管理員帳號後，從後台開啟 LIFF 測試連結。</p>
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
        <p className="text-sm text-amber-900">
          LINE Console Endpoint 為網站根目錄 <code className="text-xs">/</code>。
          請在 <strong>LINE App 內</strong> 透過下方 LIFF 連結開啟後，在根路徑{' '}
          <code className="text-xs">/?adminLiffDebug=1</code> 測試 init。
        </p>
        {!initPathOk ? (
          <p className="mt-2 text-xs text-amber-800">
            目前 pathname 為 {location.pathname}，init 按鈕已停用。請改用 LIFF 連結開啟根路徑。
          </p>
        ) : null}
      </Card>

      <Card className="space-y-2">
        <p className="text-sm font-semibold text-text-main">LIFF 測試連結（在 LINE 內開啟）</p>
        {envLiffUrl ? (
          <div className="space-y-1">
            <p className="text-xs text-text-muted">環境變數 ID</p>
            <p className="break-all text-xs text-text-main">{envLiffUrl}</p>
            <Button type="button" variant="secondary" className="w-full" onClick={() => void handleCopy(envLiffUrl, 'env LIFF URL')}>
              複製 env LIFF 連結
            </Button>
          </div>
        ) : (
          <p className="text-xs text-danger">VITE_LINE_LIFF_ID 未設定，無法產生 env LIFF 連結。</p>
        )}
        <div className="space-y-1">
          <p className="text-xs text-text-muted">硬編碼 prod ID（{HARDCODED_LIFF_ID.slice(-12)}）</p>
          <p className="break-all text-xs text-text-main">{hardcodedLiffUrl}</p>
          <Button
            type="button"
            variant="secondary"
            className="w-full"
            onClick={() => void handleCopy(hardcodedLiffUrl, 'hardcoded LIFF URL')}
          >
            複製 hardcoded LIFF 連結
          </Button>
        </div>
        {copyMessage ? <p className="text-xs text-[#2F7D4E]">{copyMessage}</p> : null}
      </Card>

      <Card className="space-y-3">
        <p className="text-sm font-semibold text-text-main">手動 liff.init()</p>
        <p className="text-xs text-text-muted">
          若已 init 過，第二次可能失敗；請按「重新整理頁面」後再測。
        </p>
        <div className="flex flex-col gap-2">
          <Button
            type="button"
            className="w-full"
            disabled={!canInit || initLoading !== null}
            onClick={() => void handleInit('env')}
          >
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
          <Button
            type="button"
            variant="secondary"
            className="w-full"
            onClick={() => window.location.reload()}
          >
            重新整理頁面
          </Button>
        </div>
        {!canInit && isAdmin && !initPathOk ? (
          <p className="text-xs text-text-subtle">init 僅能在 pathname=/ 時執行（請用上方 LIFF 連結）。</p>
        ) : null}
      </Card>

      {diagnosticText ? (
        <Card className="border-amber-200 bg-amber-50/80">
          <p className="mb-2 text-sm font-semibold text-amber-900">診斷輸出</p>
          <pre className="max-h-96 overflow-auto whitespace-pre-wrap break-all text-xs text-amber-950">
            {diagnosticText}
          </pre>
          <Button
            type="button"
            variant="secondary"
            className="mt-2 w-full"
            onClick={() => void handleCopy(diagnosticText, '診斷輸出')}
          >
            複製全部診斷
          </Button>
        </Card>
      ) : null}
    </main>
  )
}
