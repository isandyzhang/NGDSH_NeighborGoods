import { hasNestedLiffState } from '@/features/admin/liffInitDebug'
import type { ShareListingOptions } from '@/features/listings/utils/lineShare'

const STORAGE_KEY = 'neighborGoods.listingSharePending'
const LIFF_OAUTH_PRESERVE_KEYS = ['code', 'state', 'liffClientId', 'liffRedirectUri', 'liff.hback'] as const

export const hasListingShareOAuthReturnParams = (params: URLSearchParams): boolean =>
  LIFF_OAUTH_PRESERVE_KEYS.some((key) => params.has(key))

const isListingShareFlag = (params: URLSearchParams) =>
  params.get('listingShare') === '1' || params.get('listingsShare') === '1'

export const liffStateImpliesListingShare = (liffStateRaw: string): boolean => {
  let current = liffStateRaw.trim()
  for (let depth = 0; depth < 6; depth += 1) {
    try {
      const decoded = decodeURIComponent(current)
      const params = new URLSearchParams(decoded.startsWith('?') ? decoded.slice(1) : decoded)
      if (isListingShareFlag(params)) {
        return true
      }
      if (/listingShare(=|%3D)1|listingsShare(=|%3D)1/i.test(decoded)) {
        return true
      }
      const inner = params.get('liff.state')
      if (!inner || inner === current) {
        return false
      }
      current = inner
    } catch {
      return false
    }
  }
  return false
}
const PENDING_TTL_MS = 15 * 60 * 1000

export type ListingSharePendingSession = ShareListingOptions & {
  returnTo: string
  savedAt: number
}

const readRaw = (): ListingSharePendingSession | null => {
  if (typeof sessionStorage === 'undefined') {
    return null
  }

  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) {
      return null
    }

    const parsed = JSON.parse(raw) as ListingSharePendingSession
    if (!parsed?.listingId?.trim() || !parsed?.listingTitle?.trim() || typeof parsed.savedAt !== 'number') {
      sessionStorage.removeItem(STORAGE_KEY)
      return null
    }

    if (Date.now() - parsed.savedAt > PENDING_TTL_MS) {
      sessionStorage.removeItem(STORAGE_KEY)
      return null
    }

    return parsed
  } catch {
    sessionStorage.removeItem(STORAGE_KEY)
    return null
  }
}

export const saveListingSharePending = (options: ShareListingOptions, returnTo: string) => {
  if (!options.listingId.trim() || !options.listingTitle.trim() || typeof sessionStorage === 'undefined') {
    return
  }

  const payload: ListingSharePendingSession = {
    ...options,
    returnTo: returnTo.startsWith('/') ? returnTo : '/listings',
    savedAt: Date.now(),
  }
  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(payload))
}

export const readListingSharePending = (): ListingSharePendingSession | null => readRaw()

export const hasListingSharePending = (): boolean => readRaw() !== null

export const clearListingSharePending = () => {
  if (typeof sessionStorage !== 'undefined') {
    sessionStorage.removeItem(STORAGE_KEY)
  }
}

const liffStateQueryParams = (liffStateRaw: string): URLSearchParams | null => {
  const decoded = decodeURIComponent(liffStateRaw.trim())
  if (!decoded) {
    return null
  }

  const normalized = decoded.startsWith('?') ? decoded.slice(1) : decoded
  const qIndex = normalized.indexOf('?')
  if (qIndex >= 0 && normalized.startsWith('/')) {
    return new URLSearchParams(normalized.slice(qIndex + 1))
  }

  if (!normalized.startsWith('/')) {
    return new URLSearchParams(normalized)
  }

  return null
}

/** 已在 LINE WebView 且 URL 被重複包裝 liff.state 時，改為乾淨的 /?listingShare=1 */
export const listingShareEntryNeedsCleanup = (pathname: string, search: string): boolean => {
  if (pathname !== '/') {
    return false
  }

  const normalized = search.startsWith('?') ? search : search ? `?${search}` : ''
  if (!normalized) {
    return false
  }

  if (hasNestedLiffState(normalized)) {
    return hasListingSharePending() || liffStateImpliesListingShare(new URLSearchParams(normalized.slice(1)).get('liff.state') ?? '')
  }

  const params = new URLSearchParams(normalized.slice(1))
  const liffState = params.get('liff.state') ?? ''
  if (liffState && liffStateImpliesListingShare(liffState) && !isListingShareFlag(params)) {
    return hasListingSharePending()
  }

  return false
}

export const buildCleanListingShareEntrySearch = (search: string): string => {
  const raw = search.startsWith('?') ? search.slice(1) : search
  const params = new URLSearchParams(raw)
  const clean = new URLSearchParams()
  clean.set('listingShare', '1')

  for (const key of LIFF_OAUTH_PRESERVE_KEYS) {
    const value = params.get(key)
    if (value) {
      clean.set(key, value)
    }
  }

  return `?${clean.toString()}`
}

export const resolveListingShareParams = (
  search: string,
): (ShareListingOptions & { returnTo: string }) | null => {
  const params = new URLSearchParams(search.startsWith('?') ? search.slice(1) : search)
  const liffStateParams = params.get('liff.state') ? liffStateQueryParams(params.get('liff.state') ?? '') : null

  const lineAction = params.get('lineAction') ?? liffStateParams?.get('lineAction')
  const listingShareFlag = params.get('listingShare') ?? liffStateParams?.get('listingShare')
  if (lineAction?.trim() && listingShareFlag !== '1') {
    return null
  }

  if (isListingShareFlag(params) && !params.get('listingId')?.trim() && !liffStateParams?.get('listingId')?.trim()) {
    const pending = readListingSharePending()
    if (pending) {
      return pending
    }
  }

  if (
    hasListingShareOAuthReturnParams(params) &&
    !params.get('listingId')?.trim() &&
    !liffStateParams?.get('listingId')?.trim()
  ) {
    return readListingSharePending()
  }

  const listingId = params.get('listingId') ?? liffStateParams?.get('listingId') ?? ''
  const listingTitle = params.get('title') ?? liffStateParams?.get('title') ?? ''
  if (!listingId.trim() || !listingTitle.trim()) {
    const liffStateRaw = params.get('liff.state') ?? ''
    const isShareFlow =
      isListingShareFlag(params) ||
      liffStateImpliesListingShare(liffStateRaw) ||
      listingShareFlag === '1'
    if (!isShareFlow) {
      return null
    }
    return readListingSharePending()
  }

  const options: ShareListingOptions & { returnTo: string } = {
    listingId: listingId.trim(),
    listingTitle: listingTitle.trim(),
    priceLabel: params.get('price') ?? liffStateParams?.get('price') ?? undefined,
    residenceName: params.get('residence') ?? liffStateParams?.get('residence') ?? undefined,
    imageUrl: params.get('imageUrl') ?? liffStateParams?.get('imageUrl') ?? undefined,
    categoryName: params.get('category') ?? liffStateParams?.get('category') ?? undefined,
    conditionName: params.get('condition') ?? liffStateParams?.get('condition') ?? undefined,
    returnTo: params.get('returnTo')?.startsWith('/')
      ? (params.get('returnTo') as string)
      : liffStateParams?.get('returnTo')?.startsWith('/')
        ? (liffStateParams.get('returnTo') as string)
        : `/listings/${listingId.trim()}`,
  }

  saveListingSharePending(options, options.returnTo)
  return options
}

/**
 * LINE 有時把 liff.line.me 的 query 變成站內 path，例如
 * /listingShare=1&listingId=... → 應修正為 /?listingShare=1&listingId=...
 */
export const getListingShareRedirectFromBrokenPath = (
  pathname: string,
  search: string,
): { pathname: string; search: string } | null => {
  if (pathname === '/') {
    return null
  }

  const pathBody = pathname.startsWith('/') ? pathname.slice(1) : pathname
  const searchBody = search.startsWith('?') ? search.slice(1) : search
  const looksLikeSharePath =
    pathBody.startsWith('listingShare=') ||
    pathBody.includes('&listingId=') ||
    pathBody.includes('listingId=')
  const looksLikeShareSearch = searchBody.includes('listingShare=') || searchBody.includes('listingId=')

  if (!looksLikeSharePath && !looksLikeShareSearch) {
    return null
  }

  const combined = [pathBody, searchBody].filter(Boolean).join('&')
  const params = new URLSearchParams(combined)
  if (!params.get('listingShare')) {
    params.set('listingShare', '1')
  }

  if (!params.get('listingId')?.trim() && !hasListingSharePending()) {
    return null
  }

  return { pathname: '/', search: `?${params.toString()}` }
}

export const buildListingShareRootSearch = (options: ShareListingOptions, returnTo: string): string => {
  const params = new URLSearchParams({
    listingShare: '1',
    listingId: options.listingId,
    title: options.listingTitle,
    returnTo: returnTo.startsWith('/') ? returnTo : '/listings',
  })
  if (options.priceLabel) {
    params.set('price', options.priceLabel)
  }
  if (options.categoryName) {
    params.set('category', options.categoryName)
  }
  if (options.conditionName) {
    params.set('condition', options.conditionName)
  }
  if (options.residenceName) {
    params.set('residence', options.residenceName)
  }
  if (options.imageUrl) {
    params.set('imageUrl', options.imageUrl)
  }
  return `?${params.toString()}`
}
