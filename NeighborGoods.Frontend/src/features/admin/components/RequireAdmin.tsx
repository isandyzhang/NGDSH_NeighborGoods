import { useEffect, useState } from 'react'
import { Navigate, Outlet } from 'react-router-dom'
import { accountApi } from '@/features/account/api/accountApi'
import { useAuth } from '@/features/auth/components/AuthProvider'
import { ADMIN_ROLE_CODE } from '@/features/admin/constants/adminRole'

export const RequireAdmin = () => {
  const { isAuthenticated, tokens } = useAuth()
  const [role, setRole] = useState<number | null>(tokens?.role ?? null)
  const [loading, setLoading] = useState(Boolean(isAuthenticated && role === null))

  useEffect(() => {
    if (!isAuthenticated) {
      setLoading(false)
      setRole(null)
      return
    }

    if (tokens?.role !== undefined) {
      setRole(tokens.role)
      setLoading(false)
      return
    }

    let disposed = false
    setLoading(true)

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
          setLoading(false)
        }
      })

    return () => {
      disposed = true
    }
  }, [isAuthenticated, tokens?.role])

  if (loading) {
    return <div className="px-4 py-8 text-sm text-text-subtle">正在驗證管理員權限...</div>
  }

  if (role !== ADMIN_ROLE_CODE) {
    return <Navigate to="/listings" replace />
  }

  return <Outlet />
}
