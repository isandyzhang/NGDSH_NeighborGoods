import { hasLineBindingPending } from '@/features/account/lineBindingSession'

const isSafeInternalPath = (path: string) => path.startsWith('/') && !path.startsWith('//')

const buildLineNotifyTarget = (bindToken: string, botLink: string) =>
  `/liff/line-notify?bindToken=${encodeURIComponent(bindToken)}&botLink=${encodeURIComponent(botLink)}`

const parseLiffStateTarget = (liffState: string): string | null => {
  const decoded = decodeURIComponent(liffState.trim())
  if (!decoded) {
    return null
  }

  const normalized = decoded.startsWith('?') ? decoded.slice(1) : decoded
  const qIndex = normalized.indexOf('?')
  const path = qIndex >= 0 ? normalized.slice(0, qIndex) : normalized
  const query = qIndex >= 0 ? normalized.slice(qIndex) : ''

  if (isSafeInternalPath(path)) {
    return `${path}${query}`
  }

  // liff.state may carry query-only params (e.g. bindToken=...&botLink=...)
  const params = new URLSearchParams(normalized)
  const bindToken = params.get('bindToken')
  if (bindToken) {
    return buildLineNotifyTarget(bindToken, params.get('botLink') ?? '')
  }

  return null
}

/** LIFF Endpoint is site root; binding must run on `/` (not `/liff/line-notify`) for liff.init. */
export const isLineNotifyBindingEntry = (pathname: string, search: string): boolean => {
  const params = new URLSearchParams(search)
  if (
    params.get('bindToken') &&
    (pathname === '/' || pathname === '/liff' || pathname === '/liff/line-notify')
  ) {
    return true
  }

  const liffState = params.get('liff.state')
  if (liffState) {
    const target = parseLiffStateTarget(liffState)
    if (target?.startsWith('/liff/line-notify')) {
      return true
    }
  }

  // OAuth return (?code=...) drops bindToken from URL; session keeps binding on `/`.
  return pathname === '/' && hasLineBindingPending()
}

/** Redirect URI for liff.login — root only; bindToken lives in sessionStorage across OAuth. */
export const buildLineNotifyBindingLoginRedirectUri = () => {
  const origin = typeof window !== 'undefined' ? window.location.origin : 'https://www.neighborgoodstw.com'
  return `${origin}/`
}

export const resolveLiffEntryTarget = (pathname: string, search: string): string | null => {
  if (isLineNotifyBindingEntry(pathname, search)) {
    return null
  }

  const params = new URLSearchParams(search)
  const liffState = params.get('liff.state')
  if (liffState) {
    const target = parseLiffStateTarget(liffState)
    if (target) {
      return target
    }
  }

  return null
}
