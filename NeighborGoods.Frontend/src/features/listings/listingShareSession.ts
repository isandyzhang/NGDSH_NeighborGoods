import type { ShareListingOptions } from '@/features/listings/utils/lineShare'

const STORAGE_KEY = 'neighborGoods.listingSharePending'
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

export const resolveListingShareParams = (
  search: string,
): (ShareListingOptions & { returnTo: string }) | null => {
  const params = new URLSearchParams(search.startsWith('?') ? search.slice(1) : search)
  const liffStateParams = params.get('liff.state') ? liffStateQueryParams(params.get('liff.state') ?? '') : null

  const listingId = params.get('listingId') ?? liffStateParams?.get('listingId') ?? ''
  const listingTitle = params.get('title') ?? liffStateParams?.get('title') ?? ''
  if (!listingId.trim() || !listingTitle.trim()) {
    const pending = readListingSharePending()
    return pending
  }

  const options: ShareListingOptions & { returnTo: string } = {
    listingId: listingId.trim(),
    listingTitle: listingTitle.trim(),
    priceLabel: params.get('price') ?? liffStateParams?.get('price') ?? undefined,
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
  return `?${params.toString()}`
}
