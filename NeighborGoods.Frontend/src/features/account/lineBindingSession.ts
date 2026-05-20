/** Aligns with backend LineBindingPending TTL (AccountLineBindingService.BindingTokenTtl). */
export const LINE_BINDING_PENDING_TTL_MS = 15 * 60 * 1000

const STORAGE_KEY = 'neighborGoods.lineBindingPending'

export type LineBindingPendingSession = {
  bindToken: string
  botLink?: string
  savedAt: number
}

const readRaw = (): LineBindingPendingSession | null => {
  if (typeof sessionStorage === 'undefined') {
    return null
  }

  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    if (!raw) {
      return null
    }

    const parsed = JSON.parse(raw) as LineBindingPendingSession
    if (!parsed?.bindToken || typeof parsed.savedAt !== 'number') {
      sessionStorage.removeItem(STORAGE_KEY)
      return null
    }

    if (Date.now() - parsed.savedAt > LINE_BINDING_PENDING_TTL_MS) {
      sessionStorage.removeItem(STORAGE_KEY)
      return null
    }

    return parsed
  } catch {
    sessionStorage.removeItem(STORAGE_KEY)
    return null
  }
}

export const saveLineBindingPending = (bindToken: string, botLink?: string) => {
  if (!bindToken.trim() || typeof sessionStorage === 'undefined') {
    return
  }

  const payload: LineBindingPendingSession = {
    bindToken: bindToken.trim(),
    savedAt: Date.now(),
  }
  if (botLink?.trim()) {
    payload.botLink = botLink.trim()
  }

  sessionStorage.setItem(STORAGE_KEY, JSON.stringify(payload))
}

export const readLineBindingPending = (): LineBindingPendingSession | null => readRaw()

export const hasLineBindingPending = (): boolean => readRaw() !== null

export const clearLineBindingPending = () => {
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

/** URL bindToken > session > liff.state; persists URL token to session when present. */
export const resolveLineBindingParams = (search: string): { bindToken: string; botLink: string } => {
  const params = new URLSearchParams(search)
  const liffStateRaw = params.get('liff.state') ?? ''
  const liffStateParams = liffStateRaw ? liffStateQueryParams(liffStateRaw) : null

  let bindToken = params.get('bindToken') ?? liffStateParams?.get('bindToken') ?? ''
  let botLink = params.get('botLink') ?? liffStateParams?.get('botLink') ?? ''

  if (bindToken) {
    saveLineBindingPending(bindToken, botLink)
    return { bindToken, botLink }
  }

  const pending = readLineBindingPending()
  if (pending) {
    return {
      bindToken: pending.bindToken,
      botLink: botLink || pending.botLink || '',
    }
  }

  return { bindToken: '', botLink }
}
