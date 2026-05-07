import { useEffect, useState } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from './AuthProvider'

export const RequireAuth = () => {
  const { isAuthenticated, tokens, refreshTokens } = useAuth()
  const location = useLocation()
  const from = `${location.pathname}${location.search}${location.hash}`
  const [checking, setChecking] = useState(true)
  const [authorized, setAuthorized] = useState(false)

  useEffect(() => {
    let disposed = false

    const ensureSession = async () => {
      if (!isAuthenticated || !tokens) {
        if (!disposed) {
          setAuthorized(false)
          setChecking(false)
        }
        return
      }

      const expiresAt = Date.parse(tokens.accessTokenExpiresAt)
      const hasValidExpire = Number.isFinite(expiresAt)
      const isExpired = hasValidExpire ? expiresAt <= Date.now() : false
      if (!isExpired) {
        if (!disposed) {
          setAuthorized(true)
          setChecking(false)
        }
        return
      }

      const refreshed = await refreshTokens()
      if (!disposed) {
        setAuthorized(Boolean(refreshed?.accessToken))
        setChecking(false)
      }
    }

    void ensureSession()
    return () => {
      disposed = true
    }
  }, [isAuthenticated, refreshTokens, tokens])

  if (checking) {
    return <div className="px-4 py-8 text-sm text-text-subtle">正在驗證登入狀態...</div>
  }

  if (!authorized) {
    return <Navigate to={`/login?from=${encodeURIComponent(from)}`} replace state={{ from }} />
  }

  return <Outlet />
}
