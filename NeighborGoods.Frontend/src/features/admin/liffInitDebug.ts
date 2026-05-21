import liff from '@line/liff'

export const HARDCODED_LIFF_ID = '2008745853-Ui8PkOGi'

export const ENV_LIFF_ID = import.meta.env.VITE_LINE_LIFF_ID as string | undefined

export type LiffInitSource = 'env' | 'hardcoded'

export type LiffPreInitSnapshot = {
  capturedAt: string
  href: string
  origin: string
  pathname: string
  search: string
  hasLiffState: boolean
  hasOAuthCode: boolean
  userAgent: string
  viteMode: string
  envLiffIdConfigured: boolean
  envLiffIdSuffix: string
  hardcodedLiffIdSuffix: string
  liffIdsMatch: boolean
  initPathOk: boolean
}

export type LiffInitAttemptResult = {
  source: LiffInitSource
  liffIdSuffix: string
  ok: boolean
  durationMs: number
  errorName: string | null
  errorCode: string | null
  errorMessage: string | null
}

export type LiffPostInitSnapshot = {
  isInClient: boolean
  isLoggedIn: boolean
  version: string | null
  os: string | null
  language: string | null
  contextType: string | null
  apiGetFriendship: boolean
  apiShareTargetPicker: boolean
  apiSendMessages: boolean
  idTokenPresent: boolean
  idTokenPrefix: string | null
  profileUserId: string | null
  profileDisplayName: string | null
  profileError: string | null
}

const liffIdSuffix = (id: string) => (id.length > 12 ? id.slice(-12) : id)

const formatError = (err: unknown): { name: string | null; code: string | null; message: string } => {
  if (err instanceof Error) {
    const code =
      typeof err === 'object' && err && 'code' in err ? String((err as { code: unknown }).code) : null
    return { name: err.name, code, message: err.message }
  }
  if (typeof err === 'object' && err) {
    const code = 'code' in err ? String((err as { code: unknown }).code) : null
    const message = 'message' in err ? String((err as { message: unknown }).message) : String(err)
    const name = 'name' in err ? String((err as { name: unknown }).name) : null
    return { name, code, message }
  }
  return { name: null, code: null, message: String(err) }
}

export const collectPreInitSnapshot = (): LiffPreInitSnapshot => {
  const envTrimmed = ENV_LIFF_ID?.trim() ?? ''
  const params =
    typeof window !== 'undefined'
      ? new URLSearchParams(window.location.search)
      : new URLSearchParams()

  return {
    capturedAt: new Date().toISOString(),
    href: typeof window !== 'undefined' ? window.location.href : '',
    origin: typeof window !== 'undefined' ? window.location.origin : '',
    pathname: typeof window !== 'undefined' ? window.location.pathname : '',
    search: typeof window !== 'undefined' ? window.location.search : '',
    hasLiffState: params.has('liff.state'),
    hasOAuthCode: params.has('code'),
    userAgent: typeof navigator !== 'undefined' ? navigator.userAgent : '',
    viteMode: import.meta.env.MODE,
    envLiffIdConfigured: Boolean(envTrimmed),
    envLiffIdSuffix: envTrimmed ? liffIdSuffix(envTrimmed) : '(none)',
    hardcodedLiffIdSuffix: liffIdSuffix(HARDCODED_LIFF_ID),
    liffIdsMatch: Boolean(envTrimmed) && envTrimmed === HARDCODED_LIFF_ID,
    initPathOk: typeof window !== 'undefined' && window.location.pathname === '/',
  }
}

export const runLiffInitAttempt = async (
  liffId: string,
  source: LiffInitSource,
): Promise<LiffInitAttemptResult> => {
  const trimmed = liffId.trim()
  const started = performance.now()
  try {
    await liff.init({ liffId: trimmed })
    return {
      source,
      liffIdSuffix: liffIdSuffix(trimmed),
      ok: true,
      durationMs: Math.round(performance.now() - started),
      errorName: null,
      errorCode: null,
      errorMessage: null,
    }
  } catch (err) {
    const { name, code, message } = formatError(err)
    return {
      source,
      liffIdSuffix: liffIdSuffix(trimmed),
      ok: false,
      durationMs: Math.round(performance.now() - started),
      errorName: name,
      errorCode: code,
      errorMessage: message,
    }
  }
}

export const collectPostInitSnapshot = async (): Promise<LiffPostInitSnapshot> => {
  const base: LiffPostInitSnapshot = {
    isInClient: false,
    isLoggedIn: false,
    version: null,
    os: null,
    language: null,
    contextType: null,
    apiGetFriendship: false,
    apiShareTargetPicker: false,
    apiSendMessages: false,
    idTokenPresent: false,
    idTokenPrefix: null,
    profileUserId: null,
    profileDisplayName: null,
    profileError: null,
  }

  try {
    base.isInClient = liff.isInClient()
    base.isLoggedIn = liff.isLoggedIn()
    base.version = liff.getVersion()
    base.os = liff.getOS() ?? null
    base.language = liff.getLanguage() ?? null
    base.contextType = liff.getContext()?.type ?? null
    base.apiGetFriendship = liff.isApiAvailable('getFriendship')
    base.apiShareTargetPicker = liff.isApiAvailable('shareTargetPicker')
    base.apiSendMessages = liff.isApiAvailable('sendMessages')

    if (base.isLoggedIn) {
      const idToken = liff.getIDToken()
      base.idTokenPresent = Boolean(idToken)
      base.idTokenPrefix = idToken ? `${idToken.slice(0, 12)}…` : null
      try {
        const profile = await liff.getProfile()
        base.profileUserId = profile.userId
        base.profileDisplayName = profile.displayName
      } catch (err) {
        base.profileError = formatError(err).message
      }
    }
  } catch (err) {
    base.profileError = `postInit: ${formatError(err).message}`
  }

  return base
}

export const buildLiffAdminDebugUrl = (liffId: string) =>
  `https://liff.line.me/${liffId.trim()}?liff.state=${encodeURIComponent('/?adminLiffDebug=1')}`

export const formatPreInitSnapshot = (s: LiffPreInitSnapshot): string =>
  [
    `[LIFF init debug — pre] ${s.capturedAt}`,
    `href: ${s.href}`,
    `pathname: ${s.pathname} (initPathOk: ${s.initPathOk})`,
    `search: ${s.search || '(empty)'}`,
    `liff.state present: ${s.hasLiffState}`,
    `OAuth code present: ${s.hasOAuthCode}`,
    `viteMode: ${s.viteMode}`,
    `env liffId: ${s.envLiffIdConfigured ? `…${s.envLiffIdSuffix}` : 'MISSING'}`,
    `hardcoded liffId: …${s.hardcodedLiffIdSuffix}`,
    `ids match: ${s.liffIdsMatch}`,
    `userAgent: ${s.userAgent}`,
  ].join('\n')

export const formatInitAttempt = (r: LiffInitAttemptResult): string =>
  [
    `[LIFF init attempt — ${r.source}]`,
    `liffId: …${r.liffIdSuffix}`,
    `ok: ${r.ok}`,
    `durationMs: ${r.durationMs}`,
    r.errorName ? `errorName: ${r.errorName}` : null,
    r.errorCode ? `errorCode: ${r.errorCode}` : null,
    r.errorMessage ? `errorMessage: ${r.errorMessage}` : null,
  ]
    .filter(Boolean)
    .join('\n')

export const formatPostInitSnapshot = (s: LiffPostInitSnapshot): string =>
  [
    '[LIFF init debug — post]',
    `isInClient: ${s.isInClient}`,
    `isLoggedIn: ${s.isLoggedIn}`,
    `version: ${s.version ?? '-'}`,
    `os: ${s.os ?? '-'}`,
    `language: ${s.language ?? '-'}`,
    `contextType: ${s.contextType ?? '-'}`,
    `api getFriendship: ${s.apiGetFriendship}`,
    `api shareTargetPicker: ${s.apiShareTargetPicker}`,
    `api sendMessages: ${s.apiSendMessages}`,
    `idToken: ${s.idTokenPresent ? s.idTokenPrefix : 'MISSING'}`,
    `profile: ${s.profileDisplayName ?? '-'} (${s.profileUserId ?? '-'})`,
    s.profileError ? `profileError: ${s.profileError}` : null,
  ]
    .filter(Boolean)
    .join('\n')
