import liff from '@line/liff'

export const LIFF_BIND_DEBUG_SESSION_KEY = 'neighborGoods.liffBindDebug'

export type LineNotifyLiffDiagnostics = {
  capturedAt: string
  liffIdConfigured: boolean
  liffIdSuffix: string
  bindTokenPresent: boolean
  bindTokenPrefix: string
  botLink: string
  isInClient: boolean
  isLoggedIn: boolean
  getFriendshipAvailable: boolean
  friendFlag: boolean | null
  friendshipError: string | null
  profileUserId: string | null
  profileDisplayName: string | null
  idTokenPresent: boolean
  idTokenPrefix: string | null
  locationHref: string
}

export const isLineBindDebugEnabled = (search: string): boolean => {
  if (typeof window === 'undefined') {
    return false
  }

  const params = new URLSearchParams(search.startsWith('?') ? search : search ? `?${search}` : '')
  if (params.get('liffDebug') === '1') {
    return true
  }

  return sessionStorage.getItem(LIFF_BIND_DEBUG_SESSION_KEY) === '1'
}

export const enableLineBindDebugSession = () => {
  if (typeof sessionStorage !== 'undefined') {
    sessionStorage.setItem(LIFF_BIND_DEBUG_SESSION_KEY, '1')
  }
}

export const appendLiffDebugToUrl = (url: string): string => {
  if (url.includes('liffDebug=1')) {
    return url
  }
  const sep = url.includes('?') ? '&' : '?'
  return `${url}${sep}liffDebug=1`
}

const formatError = (err: unknown) => (err instanceof Error ? err.message : String(err))

export const collectLineNotifyLiffDiagnostics = async (
  liffId: string | undefined,
  bindToken: string,
  botLink: string,
): Promise<LineNotifyLiffDiagnostics> => {
  const base: LineNotifyLiffDiagnostics = {
    capturedAt: new Date().toISOString(),
    liffIdConfigured: Boolean(liffId?.trim()),
    liffIdSuffix: liffId?.trim() ? liffId.trim().slice(-12) : '(none)',
    bindTokenPresent: Boolean(bindToken),
    bindTokenPrefix: bindToken ? `${bindToken.slice(0, 8)}…` : '(none)',
    botLink: botLink || '(none)',
    isInClient: false,
    isLoggedIn: false,
    getFriendshipAvailable: false,
    friendFlag: null,
    friendshipError: null,
    profileUserId: null,
    profileDisplayName: null,
    idTokenPresent: false,
    idTokenPrefix: null,
    locationHref: typeof window !== 'undefined' ? window.location.href : '',
  }

  try {
    base.isInClient = liff.isInClient()
    base.isLoggedIn = liff.isLoggedIn()
    base.getFriendshipAvailable = liff.isApiAvailable('getFriendship')

    if (base.isLoggedIn) {
      const idToken = liff.getIDToken()
      base.idTokenPresent = Boolean(idToken)
      base.idTokenPrefix = idToken ? `${idToken.slice(0, 12)}…` : null

      try {
        const profile = await liff.getProfile()
        base.profileUserId = profile.userId
        base.profileDisplayName = profile.displayName
      } catch (err) {
        base.friendshipError = `getProfile: ${formatError(err)}`
      }
    }

    if (base.getFriendshipAvailable) {
      try {
        const f = await liff.getFriendship()
        base.friendFlag = f.friendFlag
      } catch (err) {
        base.friendshipError = `getFriendship: ${formatError(err)}`
      }
    } else {
      base.friendshipError = 'getFriendship API 不可用（常見：Login channel 未 Link a bot）'
    }
  } catch (err) {
    base.friendshipError = `diagnostics: ${formatError(err)}`
  }

  return base
}

export const formatLineNotifyLiffDiagnosticsLog = (d: LineNotifyLiffDiagnostics): string =>
  [
    `[LIFF bind debug] ${d.capturedAt}`,
    `liffId: …${d.liffIdSuffix}`,
    `bindToken: ${d.bindTokenPresent ? d.bindTokenPrefix : 'MISSING'}`,
    `botLink: ${d.botLink}`,
    `isInClient: ${d.isInClient}`,
    `isLoggedIn: ${d.isLoggedIn}`,
    `getFriendshipAvailable: ${d.getFriendshipAvailable}`,
    `friendFlag: ${d.friendFlag === null ? 'n/a' : d.friendFlag}`,
    `profile: ${d.profileDisplayName ?? '-'} (${d.profileUserId ?? '-'})`,
    `idToken: ${d.idTokenPresent ? d.idTokenPrefix : 'MISSING'}`,
    d.friendshipError ? `error: ${d.friendshipError}` : null,
    `href: ${d.locationHref}`,
  ]
    .filter(Boolean)
    .join('\n')
