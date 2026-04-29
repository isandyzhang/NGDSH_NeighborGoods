import { useEffect, useMemo, useState } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { authApi } from '@/features/auth/api/authApi'
import { useAuth } from '@/features/auth/components/AuthProvider'
import { ApiClientError } from '@/shared/types/api'
import { Card } from '@/shared/ui/Card'

const LINE_RETURN_TO_KEY = 'line_login_return_to'

export const LineLoginCallbackPage = () => {
  const navigate = useNavigate()
  const { isAuthenticated, acceptTokens } = useAuth()
  const [error, setError] = useState<string | null>(null)

  const query = useMemo(() => new URLSearchParams(window.location.search), [])
  const code = query.get('code')
  const state = query.get('state')

  useEffect(() => {
    if (!code || !state) {
      setError('LINE 登入驗證失敗：缺少必要參數')
      return
    }

    let disposed = false
    // In React StrictMode (dev), effect may run twice.
    // Defer once to avoid sending duplicate callback requests that consume state twice.
    const timerId = window.setTimeout(() => {
      void authApi
        .lineCallback(code, state)
        .then((tokens) => {
          if (disposed) {
            return
          }

          acceptTokens(tokens)
          const returnTo = sessionStorage.getItem(LINE_RETURN_TO_KEY) ?? '/listings'
          sessionStorage.removeItem(LINE_RETURN_TO_KEY)
          navigate(returnTo, { replace: true })
        })
        .catch((err) => {
          if (disposed) {
            return
          }

          const message = err instanceof ApiClientError ? err.message : 'LINE 登入失敗，請稍後再試'
          setError(message)
        })
    }, 0)

    return () => {
      disposed = true
      window.clearTimeout(timerId)
    }
  }, [acceptTokens, code, navigate, state])

  if (isAuthenticated) {
    return <Navigate to="/listings" replace />
  }

  return (
    <main className="mx-auto flex min-h-[calc(100vh-4rem)] w-full max-w-6xl items-center justify-center px-4 py-8">
      <Card className="w-full max-w-md text-center">
        <p className="mb-2 text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="text-2xl font-semibold text-text-main">LINE 登入</h1>
        {error ? (
          <p className="mt-3 text-sm text-danger">{error}</p>
        ) : (
          <p className="mt-3 text-sm text-text-subtle">驗證中，將自動跳轉...</p>
        )}
      </Card>
    </main>
  )
}
