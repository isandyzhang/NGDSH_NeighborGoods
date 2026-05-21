import { hasLineBindingPending } from '@/features/account/lineBindingSession'
import {
  hasListingSharePending,
  liffStateImpliesListingShare,
  resolveListingShareParams,
} from '@/features/listings/listingShareSession'

const isSafeInternalPath = (path: string) => path.startsWith('/') && !path.startsWith('//')

import { resolveLineLiffId } from '@/app/lineLiffId'

/**
 * Path 格式：liff.line.me/{liffId}/account?...
 * 圖文選單、Flex 站內連結；手機實測比 liff.state 穩定。
 */
export const buildLiffPathUrl = (internalPath: string, search = '') => {
  const origin = typeof window !== 'undefined' ? window.location.origin : 'https://www.neighborgoodstw.com'
  const trimmedPath = internalPath.trim()
  const path = trimmedPath.startsWith('/') ? trimmedPath : `/${trimmedPath}`
  const normalizedSearch = search.startsWith('?') ? search : search ? `?${search}` : ''
  const trimmedLiffId = resolveLineLiffId()
  if (!trimmedLiffId) {
    return `${origin}${path}${normalizedSearch}`
  }
  return `https://liff.line.me/${trimmedLiffId.trim()}${path}${normalizedSearch}`
}

/** query-only 旗標仍用 liff.state（listingShare、listingId 等，須在 / 做 liff.init） */
export const buildLiffDeepLink = (internalTarget: string) => {
  const origin = typeof window !== 'undefined' ? window.location.origin : 'https://www.neighborgoodstw.com'
  const trimmed = internalTarget.trim()
  const isQueryOnlyFlag =
    trimmed.includes('=') && !trimmed.startsWith('/') && !trimmed.includes('/')
  if (!isQueryOnlyFlag) {
    const qIndex = trimmed.indexOf('?')
    if (qIndex >= 0) {
      return buildLiffPathUrl(trimmed.slice(0, qIndex), trimmed.slice(qIndex))
    }
    return buildLiffPathUrl(trimmed.startsWith('/') ? trimmed : `/${trimmed}`)
  }

  const trimmedLiffId = resolveLineLiffId()
  const fallbackPath = `/${trimmed}`
  if (!trimmedLiffId) {
    return `${origin}${fallbackPath}`
  }
  return `https://liff.line.me/${trimmedLiffId}?liff.state=${encodeURIComponent(trimmed)}`
}

/** 商品詳情 Flex「查看商品」 */
export const buildListingDetailLiffUrl = (listingId: string, search = '?from=listings') => {
  if (!listingId.trim()) {
    return buildLiffPathUrl('/listings', search)
  }
  return buildLiffPathUrl(`/listings/${listingId.trim()}`, search)
}

const buildLineNotifyTarget = (bindToken: string, botLink: string) =>
  `/liff/line-notify?bindToken=${encodeURIComponent(bindToken)}&botLink=${encodeURIComponent(botLink)}`

/** Flex 按鈕用：勿把 /listings/uuid?... 塞進 liff.state，LINE 易丟 query 導向首頁 */
export const buildListingFlexLiffState = (listingId: string, lineAction: 'chat' | 'purchase') =>
  `listingId=${listingId}&lineAction=${lineAction}`

const buildListingTargetFromParams = (params: URLSearchParams): string | null => {
  const listingId = params.get('listingId')?.trim()
  if (!listingId) {
    return null
  }

  const lineAction = params.get('lineAction')?.trim()
  const search = lineAction ? `?lineAction=${lineAction}` : ''
  return `/listings/${listingId}${search}`
}

const parseLiffStateTarget = (liffState: string): string | null => {
  let decoded = liffState.trim()
  for (let depth = 0; depth < 4; depth += 1) {
    try {
      const next = decodeURIComponent(decoded)
      if (next === decoded) {
        break
      }
      decoded = next
    } catch {
      break
    }
  }

  if (!decoded) {
    return null
  }

  if (decoded.startsWith('http://') || decoded.startsWith('https://')) {
    try {
      const url = new URL(decoded)
      const path = url.pathname
      const search = url.search
      if (isSafeInternalPath(path)) {
        return `${path}${search}`
      }
    } catch {
      // ignore
    }
  }

  const normalized = decoded.startsWith('?') ? decoded.slice(1) : decoded

  const listingFromQuery = buildListingTargetFromParams(
    new URLSearchParams(normalized.includes('?') && !normalized.startsWith('/') ? normalized.slice(normalized.indexOf('?') + 1) : normalized),
  )
  if (listingFromQuery) {
    return listingFromQuery
  }

  const qIndex = normalized.indexOf('?')
  const path = qIndex >= 0 ? normalized.slice(0, qIndex) : normalized
  const query = qIndex >= 0 ? normalized.slice(qIndex) : ''

  const pathOnly = path.startsWith('/') ? path : path.startsWith('listings') ? `/${path}` : path

  if (pathOnly.startsWith('/listingId=')) {
    const mistaken = buildListingTargetFromParams(new URLSearchParams(pathOnly.slice(1)))
    if (mistaken) {
      return mistaken
    }
  }

  if (isSafeInternalPath(pathOnly)) {
    if (pathOnly === '/listings' || pathOnly.startsWith('/listings/')) {
      return `${pathOnly}${query}`
    }
    return `${pathOnly}${query}`
  }

  // liff.state may carry query-only params (e.g. bindToken=...&botLink=...)
  const params = new URLSearchParams(normalized)
  const bindToken = params.get('bindToken')
  if (bindToken) {
    return buildLineNotifyTarget(bindToken, params.get('botLink') ?? '')
  }

  const listingTarget = buildListingTargetFromParams(params)
  if (listingTarget) {
    return listingTarget
  }

  return null
}

/** LIFF Endpoint is site root; listing FLEX share must run on `/` for liff.init. */
export const isListingShareEntry = (pathname: string, search: string): boolean => {
  const params = new URLSearchParams(search.startsWith('?') ? search.slice(1) : search)

  if (pathname !== '/') {
    return false
  }
  const shareFlag =
    params.get('listingShare') === '1' || params.get('listingsShare') === '1'
  if (shareFlag && params.get('listingId')?.trim()) {
    return true
  }
  if (shareFlag && hasListingSharePending()) {
    return true
  }

  const liffState = params.get('liff.state')
  if (liffState) {
    const deepLinkTarget = parseLiffStateTarget(liffState)
    if (deepLinkTarget?.includes('lineAction=')) {
      return false
    }
    // 僅 listingId / 商品 path 的深連結 → 商品詳情，不是 Flex 分享頁
    if (deepLinkTarget?.startsWith('/listings/') && deepLinkTarget !== '/listings') {
      return false
    }
    if (!shareFlag && !liffStateImpliesListingShare(liffState)) {
      return false
    }
    const resolved = resolveListingShareParams(`?liff.state=${encodeURIComponent(liffState)}`)
    if (resolved) {
      return true
    }
  }

  if (shareFlag) {
    return hasListingSharePending()
  }

  return false
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

  const normalizedSearch = search.startsWith('?') ? search : search ? `?${search}` : ''

  if (pathname.startsWith('/listings')) {
    return `${pathname}${normalizedSearch}`
  }

  const params = new URLSearchParams(search.startsWith('?') ? search.slice(1) : search)
  const liffState = params.get('liff.state')
  if (liffState) {
    const target = parseLiffStateTarget(liffState)
    if (target) {
      return target
    }
  }

  return null
}
