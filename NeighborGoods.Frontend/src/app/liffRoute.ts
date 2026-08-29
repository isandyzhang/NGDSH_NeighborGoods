import { hasLineBindingPending, readLineBindingPending } from '@/features/account/lineBindingSession'
import {
  hasListingSharePending,
  hasListingShareOAuthReturnParams,
  liffStateImpliesListingShare,
  readListingSharePending,
  resolveListingShareParams,
} from '@/features/listings/listingShareSession'

const isSafeInternalPath = (path: string) => path.startsWith('/') && !path.startsWith('//')

import { resolveLineLiffId } from '@/app/lineLiffId'

const normalizeLiffQuery = (search: string) =>
  !search || search === '?' ? '' : search.startsWith('?') ? search : `?${search}`

/**
 * path 格式：liff.line.me/{liffId}/listings（圖文選單、Flex 按鈕；手機實測較穩）。
 */
export const buildLiffPathUrl = (internalPath: string, search = '') => {
  const origin = typeof window !== 'undefined' ? window.location.origin : 'https://www.neighborgoodstw.com'
  const path = internalPath.trim().startsWith('/') ? internalPath.trim() : `/${internalPath.trim()}`
  const normalizedSearch = normalizeLiffQuery(search)
  const trimmedLiffId = resolveLineLiffId()
  if (!trimmedLiffId) {
    return `${origin}${path}${normalizedSearch}`
  }
  return `https://liff.line.me/${trimmedLiffId.trim()}${path}${normalizedSearch}`
}

/** query-only 旗標仍用 liff.state（listingShare=1、adminLiffDebug 等）。 */
export const buildLiffDeepLink = (internalTarget: string) => {
  const origin = typeof window !== 'undefined' ? window.location.origin : 'https://www.neighborgoodstw.com'
  const trimmed = internalTarget.trim()
  const liffState = (() => {
    if (trimmed.startsWith('/')) {
      return trimmed
    }
    if (trimmed.includes('=') && !trimmed.includes('/')) {
      return trimmed
    }
    return `/${trimmed}`
  })()
  if (liffState.startsWith('/')) {
    const qIndex = liffState.indexOf('?')
    const path = qIndex >= 0 ? liffState.slice(0, qIndex) : liffState
    const search = qIndex >= 0 ? liffState.slice(qIndex) : ''
    return buildLiffPathUrl(path, search)
  }
  const trimmedLiffId = resolveLineLiffId()
  if (!trimmedLiffId) {
    return `${origin}/${liffState}`
  }
  return `https://liff.line.me/${trimmedLiffId}?liff.state=${encodeURIComponent(liffState)}`
}

/**
 * 商品詳情 Flex 按鈕用：liff.line.me/{liffId}/listings/{id}?...
 * 手機實測比 ?liff.state=/listings/{id} 穩定（少經 RootEntry 解析）。
 */
export const buildListingDetailLiffUrl = (listingId: string, search = '?from=listings') => {
  const origin = typeof window !== 'undefined' ? window.location.origin : 'https://www.neighborgoodstw.com'
  const path = `/listings/${listingId.trim()}`
  const normalizedSearch = search.startsWith('?') ? search : search ? `?${search}` : '?from=listings'
  const trimmedLiffId = resolveLineLiffId()
  if (!trimmedLiffId || !listingId.trim()) {
    return `${origin}${path}${normalizedSearch}`
  }
  return `https://liff.line.me/${trimmedLiffId.trim()}${path}${normalizedSearch}`
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

const isLatestPendingLiffFlowListingShare = (): boolean => {
  const listingPending = readListingSharePending()
  if (!listingPending) {
    return false
  }

  const bindingPending = readLineBindingPending()
  return !bindingPending || listingPending.savedAt >= bindingPending.savedAt
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

  if (hasListingShareOAuthReturnParams(params)) {
    return isLatestPendingLiffFlowListingShare()
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

  // OAuth return (?code=...) drops bindToken/listingShare from URL; resume the newest pending flow.
  if (pathname !== '/' || !hasLineBindingPending()) {
    return false
  }

  if (hasListingShareOAuthReturnParams(params) && isLatestPendingLiffFlowListingShare()) {
    return false
  }

  return true
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
