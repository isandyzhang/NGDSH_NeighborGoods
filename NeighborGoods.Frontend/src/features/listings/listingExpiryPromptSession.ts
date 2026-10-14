const STORAGE_KEY = 'ng_listing_expiry_prompt'

const listeners = new Set<() => void>()

let loadPromise: Promise<unknown> | null = null

export const LISTING_EXPIRY_UPDATED_EVENT = 'neighborgoods:listing-expiry-updated'

export const armListingExpiryPrompt = () => {
  loadPromise = null
  sessionStorage.setItem(STORAGE_KEY, '1')
  listeners.forEach((listener) => listener())
}

export const clearListingExpiryPrompt = () => {
  loadPromise = null
  sessionStorage.removeItem(STORAGE_KEY)
}

export const finishListingExpiryPrompt = () => {
  loadPromise = null
}

export const beginListingExpiryPrompt = <T>(load: () => Promise<T>): Promise<T> | null => {
  if (sessionStorage.getItem(STORAGE_KEY) === '1') {
    sessionStorage.removeItem(STORAGE_KEY)
    const promise = load()
    loadPromise = promise
    return promise
  }

  return (loadPromise as Promise<T> | null) ?? null
}

export const subscribeListingExpiryPrompt = (listener: () => void) => {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

export const notifyListingExpiryUpdated = () => {
  window.dispatchEvent(new CustomEvent(LISTING_EXPIRY_UPDATED_EVENT))
}
