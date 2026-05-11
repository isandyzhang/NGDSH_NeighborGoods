import { useEffect } from 'react'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { TopNav } from '@/shared/ui/TopNav'

const resolveLiffStateTarget = (rawState: string): string | null => {
  const normalized = rawState.startsWith('?') ? rawState.slice(1) : rawState
  const decoded = decodeURIComponent(normalized).trim()
  if (!decoded) {
    return null
  }

  if (decoded.startsWith('http://') || decoded.startsWith('https://')) {
    try {
      const parsed = new URL(decoded)
      if (parsed.origin !== window.location.origin) {
        return null
      }
      return `${parsed.pathname}${parsed.search}${parsed.hash}`
    } catch {
      return null
    }
  }

  if (decoded.startsWith('/')) {
    return decoded
  }

  return `/${decoded}`
}

export const AppLayout = () => {
  const navigate = useNavigate()
  const location = useLocation()

  useEffect(() => {
    const params = new URLSearchParams(location.search)
    const rawLiffState = params.get('liff.state')
    if (!rawLiffState) {
      return
    }

    const target = resolveLiffStateTarget(rawLiffState)
    if (!target) {
      return
    }

    navigate(target, { replace: true })
  }, [location.search, navigate])

  return (
    <div className="min-h-screen bg-bg text-text-main">
      <TopNav />
      <Outlet />
    </div>
  )
}
