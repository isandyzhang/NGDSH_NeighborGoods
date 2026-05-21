import { hasLineBindingPending } from '@/features/account/lineBindingSession'
import { hasListingSharePending, resolveListingShareParams } from '@/features/listings/listingShareSession'

const isSafeInternalPath = (path: string) => path.startsWith('/') && !path.startsWith('//')

import { resolveLineLiffId } from '@/app/lineLiffId'

/** 在 LINE 內開啟站內路徑（透過 liff.state 深層連結） */
export const buildLiffDeepLink = (internalTarget: string) => {
  const origin = typeof window !== 'undefined' ? window.location.origin : 'https://www.neighborgoodstw.com'
  const path = internalTarget.startsWith('/') ? internalTarget : `/${internalTarget}`
  const trimmedLiffId = resolveLineLiffId()
  if (!trimmedLiffId) {
    return `${origin}${path}`
  }
  return `https://liff.line.me/${trimmedLiffId}?liff.state=${encodeURIComponent(path)}`
}

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

/** LIFF Endpoint is site root; listing FLEX share must run on `/` for liff.init. */
export const isListingShareEntry = (pathname: string, search: string): boolean => {
  if (pathname !== '/') {
    return false
  }

  const params = new URLSearchParams(search)
  if (params.get('listingShare') === '1' && params.get('listingId')?.trim()) {
    return true
  }

  const liffState = params.get('liff.state')
  if (liffState) {
    const resolved = resolveListingShareParams(`?liff.state=${encodeURIComponent(liffState)}`)
    if (resolved) {
      return true
    }
  }

  return hasListingSharePending()
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
