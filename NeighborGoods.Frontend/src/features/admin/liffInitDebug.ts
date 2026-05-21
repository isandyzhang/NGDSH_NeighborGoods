import liff from '@line/liff'

export const HARDCODED_LIFF_ID = '2008745853-Ui8PkOGi'

export const ADMIN_LIFF_DEBUG_SESSION_KEY = 'neighborGoods.adminLiffDebug'

export const ENV_LIFF_ID = import.meta.env.VITE_LINE_LIFF_ID as string | undefined

export const enableAdminLiffDebugSession = () => {
  if (typeof sessionStorage !== 'undefined') {
    sessionStorage.setItem(ADMIN_LIFF_DEBUG_SESSION_KEY, '1')
  }
}

export const clearAdminLiffDebugSession = () => {
  if (typeof sessionStorage !== 'undefined') {
    sessionStorage.removeItem(ADMIN_LIFF_DEBUG_SESSION_KEY)
  }
}

export const isAdminLiffDebugSessionActive = (): boolean => {
  if (typeof sessionStorage === 'undefined') {
    return false
  }
  return sessionStorage.getItem(ADMIN_LIFF_DEBUG_SESSION_KEY) === '1'
}

const LIFF_OAUTH_PRESERVE_KEYS = ['code', 'state', 'liffClientId', 'liffRedirectUri', 'liff.hback'] as const

const liffStateImpliesAdminDebug = (liffState: string): boolean => {
  let current = liffState.trim()
  for (let depth = 0; depth < 6; depth += 1) {
    try {
      if (current.includes('adminLiffDebug=1') || current.includes('adminLiffDebug%3D1')) {
        return true
      }
      const decoded = decodeURIComponent(current)
      if (decoded.includes('adminLiffDebug=1')) {
        return true
      }
      const params = new URLSearchParams(decoded.startsWith('?') ? decoded.slice(1) : decoded)
      if (params.get('adminLiffDebug') === '1') {
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

/** liff.state 被 LINE 重複包裝（常見於已在 LINE 內再點 liff.line.me） */
export const hasNestedLiffState = (search: string): boolean => {
  const raw = search.startsWith('?') ? search.slice(1) : search
  const liffState = new URLSearchParams(raw).get('liff.state') ?? ''
  return liffState.includes('liff.state') || liffState.includes('%3Fliff.state') || liffState.includes('%253F')
}

export const adminLiffDebugUrlNeedsCleanup = (search: string): boolean => {
  const raw = search.startsWith('?') ? search.slice(1) : search
  const params = new URLSearchParams(raw)
  if (!params.has('liff.state')) {
    return false
  }
  if (hasNestedLiffState(search)) {
    return true
  }
  return params.get('adminLiffDebug') !== '1' && Boolean(params.get('liff.state'))
}

/** 保留 OAuth 參數，移除巢狀 liff.state，改為乾淨的 adminLiffDebug=1 */
export const buildCleanAdminLiffDebugSearch = (search: string): string => {
  const raw = search.startsWith('?') ? search.slice(1) : search
  const params = new URLSearchParams(raw)
  const clean = new URLSearchParams()
  clean.set('adminLiffDebug', '1')
  for (const key of LIFF_OAUTH_PRESERVE_KEYS) {
    const value = params.get(key)
    if (value) {
      clean.set(key, value)
    }
  }
  return `?${clean.toString()}`
}

const isLineInAppBrowser = (): boolean => {
  if (typeof navigator === 'undefined') {
    return false
  }
  return /Line\//i.test(navigator.userAgent) || /\bLIFF\b/i.test(navigator.userAgent)
}

/** 根路徑是否應顯示 LIFF init 除錯頁（session、query、或 liff.state 皆可） */
export const isAdminLiffDebugEntry = (search: string): boolean => {
  const normalized = search.startsWith('?') ? search : search ? `?${search}` : ''
  const params = new URLSearchParams(normalized)
  if (params.get('adminLiffDebug') === '1') {
    return true
  }
  const liffState = params.get('liff.state')
  if (liffState && liffStateImpliesAdminDebug(liffState)) {
    return true
  }
  return isAdminLiffDebugSessionActive()
}

export type LiffInitSource = 'env' | 'hardcoded'

export type LiffPreInitSnapshot = {
  capturedAt: string
  href: string
  origin: string
  pathname: string
  search: string
  hasLiffState: boolean
  liffStateNested: boolean
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

export type ShareTargetPickerTestResult = {
  ok: boolean
  reason:
    | 'SENT'
    | 'CANCELLED'
    | 'NOT_LOGGED_IN'
    | 'NOT_IN_CLIENT'
    | 'PICKER_UNAVAILABLE'
    | 'LIFF_NOT_READY'
    | 'ERROR'
  contextType: string | null
  shareTargetPickerAvailable: boolean
  isInClient: boolean
  isLoggedIn: boolean
  durationMs: number
  errorCode: string | null
  errorMessage: string | null
}

const liffIdSuffix = (id: string) => (id.length > 12 ? id.slice(-12) : id)

const safeIsApiAvailable = (apiName: 'getFriendship' | 'shareTargetPicker'): boolean => {
  try {
    return liff.isApiAvailable(apiName)
  } catch {
    return false
  }
}

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
    liffStateNested: hasNestedLiffState(typeof window !== 'undefined' ? window.location.search : ''),
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
    base.apiGetFriendship = safeIsApiAvailable('getFriendship')
    base.apiShareTargetPicker = safeIsApiAvailable('shareTargetPicker')
    base.apiSendMessages = false

    if (base.isLoggedIn) {
      try {
        const idToken = liff.getIDToken()
        base.idTokenPresent = Boolean(idToken)
        base.idTokenPrefix = idToken ? `${idToken.slice(0, 12)}…` : null
      } catch (err) {
        base.profileError = `getIDToken: ${formatError(err).message}`
      }
      try {
        const profile = await liff.getProfile()
        base.profileUserId = profile.userId
        base.profileDisplayName = profile.displayName
      } catch (err) {
        const msg = formatError(err).message
        base.profileError = base.profileError ? `${base.profileError}; getProfile: ${msg}` : `getProfile: ${msg}`
      }
    }
  } catch (err) {
    base.profileError = `postInit: ${formatError(err).message}`
  }

  return base
}

/** 從外部瀏覽器開啟；liff.state 僅帶 query（勿用 /?… 避免與 LINE 重複包裝） */
export const buildLiffAdminDebugUrl = (liffId: string) =>
  `https://liff.line.me/${liffId.trim()}?liff.state=${encodeURIComponent('adminLiffDebug=1')}`

export const buildAdminTestFlexMessage = (origin?: string) => {
  const siteOrigin = origin ?? (typeof window !== 'undefined' ? window.location.origin : 'https://www.neighborgoodstw.com')
  const heroUrl = `${siteOrigin}/logo.png`
  const testedAt = new Date().toLocaleString('zh-TW', { hour12: false })

  return {
    type: 'flex' as const,
    altText: '[TEST] NeighborGoods FLEX 分享測試',
    contents: {
      type: 'bubble' as const,
      hero: {
        type: 'image' as const,
        url: heroUrl,
        size: 'full' as const,
        aspectRatio: '20:13' as const,
        aspectMode: 'cover' as const,
      },
      body: {
        type: 'box' as const,
        layout: 'vertical' as const,
        spacing: 'md' as const,
        contents: [
          {
            type: 'text' as const,
            text: 'LIFF TEST FLEX',
            weight: 'bold' as const,
            size: 'lg' as const,
            wrap: true,
          },
          {
            type: 'text' as const,
            text: `測試時間：${testedAt}`,
            size: 'sm' as const,
            color: '#666666',
            wrap: true,
          },
          {
            type: 'text' as const,
            text: '此為 shareTargetPicker 測試訊息，請選擇要傳送的對象。',
            size: 'sm' as const,
            color: '#666666',
            wrap: true,
          },
        ],
      },
      footer: {
        type: 'box' as const,
        layout: 'vertical' as const,
        spacing: 'sm' as const,
        contents: [
          {
            type: 'button' as const,
            style: 'primary' as const,
            action: {
              type: 'uri' as const,
              label: '開啟 NeighborGoods',
              uri: siteOrigin,
            },
          },
        ],
      },
    },
  }
}

/** 需先 liff.init()；會開啟 shareTargetPicker 讓使用者選擇傳送對象 */
export const runShareTargetPickerTest = async (): Promise<ShareTargetPickerTestResult> => {
  const started = performance.now()
  const base = (): ShareTargetPickerTestResult => ({
    ok: false,
    reason: 'ERROR',
    contextType: null,
    shareTargetPickerAvailable: false,
    isInClient: false,
    isLoggedIn: false,
    durationMs: Math.round(performance.now() - started),
    errorCode: null,
    errorMessage: null,
  })

  try {
    let contextType: string | null = null
    try {
      contextType = liff.getContext()?.type ?? null
    } catch {
      return { ...base(), reason: 'LIFF_NOT_READY', errorMessage: '請先按 init 成功後再測分享' }
    }

    const isInClient = liff.isInClient()
    const isLoggedIn = liff.isLoggedIn()
    const shareTargetPickerAvailable = safeIsApiAvailable('shareTargetPicker')

    if (!isLoggedIn) {
      return {
        ...base(),
        reason: 'NOT_LOGGED_IN',
        contextType,
        isInClient,
        isLoggedIn,
        shareTargetPickerAvailable,
        errorMessage: '尚未登入 LINE',
      }
    }
    if (!isInClient) {
      return {
        ...base(),
        reason: 'NOT_IN_CLIENT',
        contextType,
        isInClient,
        isLoggedIn,
        shareTargetPickerAvailable,
        errorMessage: '請在 LINE App 內開啟此頁',
      }
    }
    if (!shareTargetPickerAvailable) {
      return {
        ...base(),
        reason: 'PICKER_UNAVAILABLE',
        contextType,
        isInClient,
        isLoggedIn,
        shareTargetPickerAvailable,
        errorMessage: `此 LIFF 環境不支援 shareTargetPicker（context: ${contextType ?? '-'})`,
      }
    }

    const flexMessage = buildAdminTestFlexMessage()
    const pickerResult = await liff.shareTargetPicker([flexMessage], { isMultiple: true })
    if (pickerResult === null) {
      return {
        ok: false,
        reason: 'CANCELLED',
        contextType,
        shareTargetPickerAvailable,
        isInClient,
        isLoggedIn,
        durationMs: Math.round(performance.now() - started),
        errorCode: null,
        errorMessage: '已取消或未選擇對象',
      }
    }

    return {
      ok: true,
      reason: 'SENT',
      contextType,
      shareTargetPickerAvailable,
      isInClient,
      isLoggedIn,
      durationMs: Math.round(performance.now() - started),
      errorCode: null,
      errorMessage: null,
    }
  } catch (err) {
    const { code, message } = formatError(err)
    return {
      ...base(),
      errorCode: code,
      errorMessage: message,
    }
  }
}

export const formatShareTargetPickerTest = (r: ShareTargetPickerTestResult): string =>
  [
    '[LIFF shareTargetPicker test]',
    `ok: ${r.ok}`,
    `reason: ${r.reason}`,
    `contextType: ${r.contextType ?? '-'}`,
    `isInClient: ${r.isInClient}`,
    `isLoggedIn: ${r.isLoggedIn}`,
    `shareTargetPickerAvailable: ${r.shareTargetPickerAvailable}`,
    `durationMs: ${r.durationMs}`,
    r.errorCode ? `errorCode: ${r.errorCode}` : null,
    r.errorMessage ? `errorMessage: ${r.errorMessage}` : null,
  ]
    .filter(Boolean)
    .join('\n')

export const openLiffForAdminDebug = (liffId: string) => {
  enableAdminLiffDebugSession()
  if (typeof window !== 'undefined' && isLineInAppBrowser()) {
    window.location.assign(`${window.location.origin}/?adminLiffDebug=1`)
    return
  }
  window.location.assign(buildLiffAdminDebugUrl(liffId))
}

export const formatPreInitSnapshot = (s: LiffPreInitSnapshot): string =>
  [
    `[LIFF init debug — pre] ${s.capturedAt}`,
    `href: ${s.href}`,
    `pathname: ${s.pathname} (initPathOk: ${s.initPathOk})`,
    `search: ${s.search || '(empty)'}`,
    `liff.state present: ${s.hasLiffState}`,
    `liff.state nested: ${s.liffStateNested}`,
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
