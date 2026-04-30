import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from './AuthProvider'

export const RequireAuth = () => {
  const { isAuthenticated } = useAuth()
  const location = useLocation()
  const from = `${location.pathname}${location.search}${location.hash}`

  if (!isAuthenticated) {
    return <Navigate to={`/login?from=${encodeURIComponent(from)}`} replace state={{ from }} />
  }

  return <Outlet />
}
