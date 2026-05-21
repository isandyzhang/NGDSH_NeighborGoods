import type { SendMessagesParams } from '@liff/send-messages'
import { resolveLineLiffId } from '@/app/lineLiffId'
import { buildLiffDeepLink } from '@/app/liffRoute'
import {
  buildListingShareRootSearch,
  saveListingSharePending,
} from '@/features/listings/listingShareSession'

/** LIFF shareTargetPicker 接受的 Flex 訊息型別 */
type ListingLiffFlexMessage = Extract<SendMessagesParams[number], { type: 'flex' }>

const LINE_SHARE_BASE_URL = 'https://social-plugins.line.me/lineit/share'
const LINE_SHARE_PREFIX = '各位好厝邊大家好！我要分享一個超棒的東西，如果有興趣請來網站看看喔！'
const LINE_TEXT_SHARE_BASE_URL = 'https://line.me/R/msg/text/?'

const getLineLiffId = () => resolveLineLiffId()
const FLEX_ALT_TEXT_PREFIX = '我在NeighborGoods-社宅二手交易平台看到一個好物：'
const FLEX_TEXT_PRIMARY = '#333333'
const FLEX_TEXT_SECONDARY = '#666666'
const FLEX_BODY_BG = '#FFFFFF'
/** NeighborGoods 官方帳號加好友（LINE Add friend） */
const LINE_OFFICIAL_ADD_FRIEND_URL = 'https://lin.ee/6ZqrGei'
const MAX_TITLE_LENGTH = 40
const MAX_META_LENGTH = 24
const MAX_BADGE_LENGTH = 8
/** 視覺縮放約 75%（bubble size + 內距／字級） */
const FLEX_CARD_SCALE = 0.75
const flexPx = (value: number) => `${Math.round(value * FLEX_CARD_SCALE)}px`

const resolveFlexHeroImage = (imageUrl: string | undefined, origin: string) => {
  const trimmed = imageUrl?.trim()
  if (trimmed) {
    return trimmed
  }
  return `${origin}/logo.png`
}

type ListingFlexPayload = {
  listingId: string
  listingTitle: string
  priceLabel?: string
  categoryName?: string
  conditionName?: string
  residenceName?: string
  imageUrl?: string
}

export type ShareListingOptions = ListingFlexPayload & {
  origin?: string
}

export type ShareListingShareMode = 'flex' | 'text' | 'cancelled' | 'redirect'

export type ShareListingResult = {
  usedLiffFlex: boolean
  usedFallbackUrlShare: boolean
  shareMode: ShareListingShareMode
  fallbackUrl?: string
}

export type ShareListingFlexOnlyResult = {
  sent: boolean
  reason:
    | 'SENT'
    | 'LIFF_UNAVAILABLE'
    | 'NOT_LOGGED_IN'
    | 'NOT_IN_LINE_CLIENT'
    | 'SHARE_TARGET_PICKER_UNAVAILABLE'
    | 'USER_CANCELLED_OR_CLOSED'
    | 'LIFF_ERROR'
  errorCode?: string
  errorMessage?: string
  contextType?: string | null
}

export type LiffShareDiagnostics = {
  liffIdConfigured: boolean
  liffReady: boolean
  isInClient: boolean
  shareTargetPickerAvailable: boolean
  errorCode: string | null
  errorMessage: string | null
}

export type LiffShareRuntimeStatus = {
  liffIdConfigured: boolean
  liffReady: boolean
  isLoggedIn: boolean
  isInClient: boolean
  shareTargetPickerAvailable: boolean
  contextType: string | null
  errorCode: string | null
  errorMessage: string | null
}

export const detectLiffInClient = async (): Promise<boolean | null> => {
  try {
    const liffMod = await import('@line/liff')
    return liffMod.default.isInClient()
  } catch {
    return null
  }
}

export const buildListingUrl = (listingId: string, origin: string = window.location.origin) =>
  `${origin}/listings/${listingId}`

export const buildLineShareUrl = (listingId: string, listingTitle: string, origin?: string) => {
  const listingUrl = buildListingUrl(listingId, origin)
  const shareText = `${LINE_SHARE_PREFIX}${listingTitle} ${listingUrl}`.trim()

  const encodedUrl = encodeURIComponent(listingUrl)
  const encodedText = encodeURIComponent(shareText)

  return `${LINE_SHARE_BASE_URL}?url=${encodedUrl}&text=${encodedText}`
}

export const buildLineTextShareUrl = (listingId: string, listingTitle: string, origin?: string) => {
  const listingUrl = buildListingUrl(listingId, origin)
  const shareText = `${LINE_SHARE_PREFIX}${listingTitle} ${listingUrl}`.trim()
  return `${LINE_TEXT_SHARE_BASE_URL}${encodeURIComponent(shareText)}`
}

const trimText = (value: string | undefined, maxLength: number, fallback: string) => {
  const text = (value ?? '').trim()
  if (!text) {
    return fallback
  }
  if (text.length <= maxLength) {
    return text
  }
  return `${text.slice(0, Math.max(0, maxLength - 1))}…`
}

export const buildListingFlexMessage = ({
  listingId,
  listingTitle,
  priceLabel,
  categoryName,
  conditionName,
  residenceName,
  imageUrl,
  origin,
}: ListingFlexPayload & { origin?: string }): ListingLiffFlexMessage => {
  const siteOrigin = origin ?? (typeof window !== 'undefined' ? window.location.origin : 'https://www.neighborgoodstw.com')
  const detailUrl = buildLiffDeepLink(`listingId=${listingId}`)
  const listingsUrl = buildLiffDeepLink('/listings')
  const title = trimText(listingTitle, MAX_TITLE_LENGTH, '好物分享')
  const categoryBadge = trimText(categoryName, MAX_BADGE_LENGTH, '好物')
  const residence = trimText(residenceName, MAX_META_LENGTH, '社宅')
  const condition = trimText(conditionName, MAX_META_LENGTH, '未提供')
  const price = trimText(priceLabel, MAX_META_LENGTH, '歡迎查看商品詳情')
  const heroImage = resolveFlexHeroImage(imageUrl, siteOrigin)

  return {
    type: 'flex' as const,
    altText: `${FLEX_ALT_TEXT_PREFIX}${title}`,
    contents: {
      type: 'bubble' as const,
      size: 'kilo' as const,
      header: {
        type: 'box' as const,
        layout: 'vertical' as const,
        paddingAll: '0px',
        contents: [
          {
            type: 'box' as const,
            layout: 'horizontal' as const,
            contents: [
              {
                type: 'image' as const,
                url: heroImage,
                size: 'full' as const,
                aspectMode: 'cover' as const,
                aspectRatio: '4:3' as const,
                gravity: 'center' as const,
                flex: 1,
              },
              {
                type: 'box' as const,
                layout: 'horizontal' as const,
                contents: [
                  {
                    type: 'text' as const,
                    text: categoryBadge,
                    size: 'xs' as const,
                    color: '#ffffff',
                    align: 'center' as const,
                    gravity: 'center' as const,
                  },
                ],
                backgroundColor: '#06C755',
                paddingAll: flexPx(4),
                paddingStart: flexPx(8),
                paddingEnd: flexPx(8),
                flex: 0,
                position: 'absolute' as const,
                offsetStart: flexPx(12),
                offsetTop: flexPx(12),
                cornerRadius: '100px',
              },
            ],
          },
        ],
      },
      body: {
        type: 'box' as const,
        layout: 'vertical' as const,
        spacing: 'sm' as const,
        paddingAll: flexPx(16),
        backgroundColor: FLEX_BODY_BG,
        contents: [
          {
            type: 'text' as const,
            text: title,
            weight: 'bold' as const,
            size: 'lg' as const,
            wrap: true,
            color: FLEX_TEXT_PRIMARY,
          },
          {
            type: 'text' as const,
            text: `${residence} · ${condition}`,
            size: 'sm' as const,
            color: FLEX_TEXT_SECONDARY,
            wrap: true,
          },
          {
            type: 'text' as const,
            text: price,
            size: 'sm' as const,
            color: FLEX_TEXT_PRIMARY,
            weight: 'bold' as const,
            wrap: true,
          },
        ],
      },
      footer: {
        type: 'box' as const,
        layout: 'vertical' as const,
        spacing: 'sm' as const,
        paddingAll: flexPx(12),
        paddingTop: '0px',
        backgroundColor: FLEX_BODY_BG,
        contents: [
          {
            type: 'box' as const,
            layout: 'horizontal' as const,
            spacing: 'sm' as const,
            contents: [
              {
                type: 'button' as const,
                style: 'secondary' as const,
                height: 'sm' as const,
                flex: 1,
                action: {
                  type: 'uri' as const,
                  label: '商品列表',
                  uri: listingsUrl,
                },
              },
              {
                type: 'button' as const,
                style: 'primary' as const,
                height: 'sm' as const,
                flex: 1,
                color: '#06C755',
                action: {
                  type: 'uri' as const,
                  label: '查看商品詳情',
                  uri: detailUrl,
                },
              },
            ],
          },
          {
            type: 'text' as const,
            text: '還沒有加官方帳號嗎？馬上加入！',
            size: 'xs' as const,
            color: FLEX_TEXT_SECONDARY,
            wrap: true,
            align: 'center' as const,
            margin: 'md' as const,
            action: {
              type: 'uri' as const,
              label: '加入官方帳號',
              uri: LINE_OFFICIAL_ADD_FRIEND_URL,
            },
          },
        ],
      },
    },
  } as ListingLiffFlexMessage
}

/** 供 LINE Flex Simulator 貼上測試（範例資料） */
export const buildListingFlexSimulatorSample = () =>
  JSON.stringify(
    buildListingFlexMessage({
      listingId: '00000000-0000-0000-0000-000000000001',
      listingTitle: '二手書桌＋椅組',
      priceLabel: 'NT$ 800',
      categoryName: '家具',
      residenceName: '台北社宅',
      conditionName: '狀況良好',
      imageUrl: 'https://developers-resource.landpress.line.me/fx/clip/clip4.jpg',
      origin: 'https://www.neighborgoodstw.com',
    }).contents,
    null,
    2,
  )

let liffReadyPromise: Promise<boolean> | null = null

/** LIFF Endpoint 為 `/`；在非根路徑 init 會失敗（除錯頁在 `/` 手動 init 才會成功）。 */
export const isLiffEndpointPath = () =>
  typeof window === 'undefined' || window.location.pathname === '/'

export const resetLiffReadyCache = () => {
  liffReadyPromise = null
}

const safeSharePickerAvailable = (liff: { isApiAvailable: (api: string) => boolean }) => {
  try {
    return liff.isApiAvailable('shareTargetPicker')
  } catch {
    return false
  }
}

export const ensureLiffReady = async () => {
  const liffId = getLineLiffId()
  if (!liffId) {
    return null
  }

  const liffMod = await import('@line/liff')
  const liff = liffMod.default

  try {
    liff.getVersion()
    return liff
  } catch {
    // not initialized yet — init only on LIFF Endpoint `/` (fails on /listings/:id)
    if (!isLiffEndpointPath()) {
      return null
    }
  }

  if (!liffReadyPromise) {
    liffReadyPromise = liff
      .init({ liffId })
      .then(() => true)
      .catch(() => false)
  }
  const ok = await liffReadyPromise
  if (!ok) {
    resetLiffReadyCache()
    return null
  }
  return liff
}

/**
 * 與 admin LIFF 除錯頁相同：liff.state 只帶旗標；完整參數已在 redirect 前寫入 sessionStorage。
 * 勿把整串 listingId/title/... 塞進 liff.state，LINE 易改寫成 /listingShare=1&... path 而 404。
 * @see buildLiffAdminDebugUrl in liffInitDebug.ts
 */
export const buildListingShareLiffEntryUrl = (_options: ShareListingOptions, _returnTo: string) => {
  const liffId = getLineLiffId()
  if (!liffId) {
    return null
  }
  return `https://liff.line.me/${liffId}?liff.state=${encodeURIComponent('listingShare=1')}`
}

/**
 * 在 LINE 內改走根路徑分享。
 * 已在 LINE WebView 時用同源 /?listingShare=1，避免再開 liff.line.me 造成 liff.state 雙層嵌套。
 */
export const redirectToRootListingShare = (
  options: ShareListingOptions,
  returnTo: string,
  opts?: { alreadyInLineClient?: boolean },
) => {
  saveListingSharePending(options, returnTo)
  const origin =
    typeof window !== 'undefined' ? window.location.origin : 'https://www.neighborgoodstw.com'

  if (opts?.alreadyInLineClient) {
    window.location.assign(`${origin}/?listingShare=1`)
    return
  }

  const search = buildListingShareRootSearch(options, returnTo)
  const liffEntry = buildListingShareLiffEntryUrl(options, returnTo)
  const target = liffEntry ?? `${origin}${search}`
  window.location.assign(target)
}

const buildListingShareLoginRedirectUri = () => {
  const origin = typeof window !== 'undefined' ? window.location.origin : 'https://www.neighborgoodstw.com'
  return `${origin}/`
}

export type ListingPageLiffInitTestResult = {
  ok: boolean
  pathname: string
  liffIdSuffix: string
  isInClient: boolean
  shareTargetPickerAvailable: boolean
  isLoggedIn: boolean
  version: string | null
  errorMessage: string | null
}

/** 在「目前這個 pathname」強制嘗試 liff.init（除錯用；商品頁 /listings/:id 預期常失敗） */
export const testLiffInitOnCurrentPage = async (): Promise<ListingPageLiffInitTestResult> => {
  const liffId = getLineLiffId()
  const pathname = typeof window !== 'undefined' ? window.location.pathname : ''
  const base: ListingPageLiffInitTestResult = {
    ok: false,
    pathname,
    liffIdSuffix: liffId ? liffId.slice(-8) : '(none)',
    isInClient: false,
    shareTargetPickerAvailable: false,
    isLoggedIn: false,
    version: null,
    errorMessage: null,
  }

  if (!liffId) {
    return { ...base, errorMessage: 'VITE_LINE_LIFF_ID 未設定' }
  }

  resetLiffReadyCache()

  try {
    const liffMod = await import('@line/liff')
    const liff = liffMod.default
    await liff.init({ liffId })
    liffReadyPromise = Promise.resolve(true)

    return {
      ok: true,
      pathname,
      liffIdSuffix: liffId.slice(-8),
      isInClient: liff.isInClient(),
      shareTargetPickerAvailable: safeSharePickerAvailable(liff),
      isLoggedIn: liff.isLoggedIn(),
      version: liff.getVersion(),
      errorMessage: null,
    }
  } catch (err) {
    liffReadyPromise = Promise.resolve(false)
    return {
      ...base,
      errorMessage: err instanceof Error ? err.message : String(err),
    }
  }
}

/** 分享頁專用：一律明確 init（對齊 LiffDebugPage.runLiffInitAttempt / LineNotifyLiffPage） */
export const initLiffForFlexShare = async () => {
  const liffId = getLineLiffId()
  if (!liffId || !isLiffEndpointPath()) {
    return null
  }

  resetLiffReadyCache()
  const liffMod = await import('@line/liff')
  const liff = liffMod.default
  try {
    await liff.init({ liffId })
    liffReadyPromise = Promise.resolve(true)
    return liff
  } catch (err) {
    liffReadyPromise = Promise.resolve(false)
    console.warn('[listing flex share] liff.init failed', err)
    return null
  }
}

const openLineUrlShareWindow = (shareUrl: string) => {
  window.open(shareUrl, '_blank', 'noopener,noreferrer')
}

const openLineTextShare = (options: ShareListingOptions): ShareListingResult => {
  const fallbackUrl = buildLineTextShareUrl(options.listingId, options.listingTitle, options.origin)
  openLineUrlShareWindow(fallbackUrl)
  return {
    usedLiffFlex: false,
    usedFallbackUrlShare: true,
    shareMode: 'text',
    fallbackUrl,
  }
}

/** 文字／連結分享（不依 LIFF init，適合主按鈕） */
export const shareListingAsLineText = (options: ShareListingOptions): ShareListingResult =>
  openLineTextShare(options)

/** @deprecated 請改用 shareListingAsLineText 或 startListingFlexShare */
export const shareListingToLine = async (options: ShareListingOptions): Promise<ShareListingResult> =>
  shareListingAsLineText(options)

export type StartListingFlexShareResult =
  | { started: true }
  | { started: false; reason: 'NOT_IN_LINE_CLIENT' | 'LIFF_ID_MISSING' }

/** FLEX 選人分享：在 LINE 內一律導向 `/` 分享頁（僅該路徑可 liff.init） */
export const startListingFlexShare = async (
  options: ShareListingOptions,
  returnTo: string,
): Promise<StartListingFlexShareResult> => {
  if (!getLineLiffId()) {
    return { started: false, reason: 'LIFF_ID_MISSING' }
  }

  try {
    const liffMod = await import('@line/liff')
    if (!liffMod.default.isInClient()) {
      return { started: false, reason: 'NOT_IN_LINE_CLIENT' }
    }
  } catch {
    return { started: false, reason: 'NOT_IN_LINE_CLIENT' }
  }

  redirectToRootListingShare(options, returnTo, { alreadyInLineClient: true })
  return { started: true }
}

/** 僅在根路徑分享頁呼叫；流程對齊 admin LIFF 除錯（init → 登入 → shareTargetPicker） */
export const shareListingToLineFlexOnly = async (
  options: ShareListingOptions,
  returnTo?: string,
): Promise<ShareListingFlexOnlyResult> => {
  const liffId = getLineLiffId()
  if (!liffId) {
    return {
      sent: false,
      reason: 'LIFF_UNAVAILABLE',
      contextType: null,
      errorCode: 'LIFF_ID_MISSING',
      errorMessage: 'VITE_LINE_LIFF_ID 未設定（本機請設 .env.local）',
    }
  }

  if (!isLiffEndpointPath()) {
    return {
      sent: false,
      reason: 'LIFF_ERROR',
      contextType: null,
      errorCode: 'WRONG_PATH',
      errorMessage: 'Flex 分享須在網站根路徑 / 執行，請從商品頁重新點 Flex 分享',
    }
  }

  try {
    const liff = await initLiffForFlexShare()
    if (!liff) {
      return {
        sent: false,
        reason: 'LIFF_ERROR',
        contextType: null,
        errorCode: 'LIFF_INIT_FAILED',
        errorMessage: `LIFF 初始化失敗（liffId: ${liffId}）。請比照除錯頁從 liff.line.me 開啟`,
      }
    }

    if (!liff.isInClient()) {
      return {
        sent: false,
        reason: 'NOT_IN_LINE_CLIENT',
        contextType: null,
        errorMessage: '請在 LINE App 內開啟後再使用 Flex 分享',
      }
    }

    const contextType = liff.getContext()?.type ?? null

    if (!liff.isLoggedIn()) {
      const back =
        returnTo?.startsWith('/') ? returnTo : `/listings/${options.listingId}`
      saveListingSharePending(options, back)
      liff.login({ redirectUri: buildListingShareLoginRedirectUri() })
      return {
        sent: false,
        reason: 'NOT_LOGGED_IN',
        contextType,
        errorMessage: '需要 LINE 登入，已導向登入；完成後會繼續分享',
      }
    }

    if (!safeSharePickerAvailable(liff)) {
      return {
        sent: false,
        reason: 'SHARE_TARGET_PICKER_UNAVAILABLE',
        contextType,
        errorMessage: `此環境不支援 shareTargetPicker（context: ${contextType ?? '-'}）`,
      }
    }

    const flexMessage = buildListingFlexMessage(options)
    const pickerResult = await liff.shareTargetPicker([flexMessage], { isMultiple: true })
    if (pickerResult === null) {
      return { sent: false, reason: 'USER_CANCELLED_OR_CLOSED', contextType }
    }

    return { sent: true, reason: 'SENT', contextType }
  } catch (err) {
    const errorMessage = err instanceof Error ? err.message : 'Flex 分享發生未知錯誤'
    return {
      sent: false,
      reason: 'LIFF_ERROR',
      contextType: null,
      errorCode: 'LIFF_SHARE_EXCEPTION',
      errorMessage,
    }
  }
}

export const getLiffShareDiagnostics = async (): Promise<LiffShareDiagnostics> => {
  if (!getLineLiffId()) {
    return {
      liffIdConfigured: false,
      liffReady: false,
      isInClient: false,
      shareTargetPickerAvailable: false,
      errorCode: 'LIFF_ID_MISSING',
      errorMessage: 'VITE_LINE_LIFF_ID 未設定',
    }
  }

  try {
    const liff = await ensureLiffReady()
    if (!liff) {
      return {
        liffIdConfigured: true,
        liffReady: false,
        isInClient: false,
        shareTargetPickerAvailable: false,
        errorCode: 'LIFF_INIT_FAILED',
        errorMessage: 'LIFF 初始化失敗',
      }
    }

    const isInClient = liff.isInClient()
    const shareTargetPickerAvailable = safeSharePickerAvailable(liff)
    return {
      liffIdConfigured: true,
      liffReady: true,
      isInClient,
      shareTargetPickerAvailable,
      errorCode: null,
      errorMessage: null,
    }
  } catch (err) {
    const errorMessage = err instanceof Error ? err.message : 'LIFF 診斷發生未知錯誤'
    return {
      liffIdConfigured: true,
      liffReady: false,
      isInClient: false,
      shareTargetPickerAvailable: false,
      errorCode: 'LIFF_DIAGNOSTIC_EXCEPTION',
      errorMessage,
    }
  }
}

export const getLiffShareRuntimeStatus = async (): Promise<LiffShareRuntimeStatus> => {
  if (!getLineLiffId()) {
    return {
      liffIdConfigured: false,
      liffReady: false,
      isLoggedIn: false,
      isInClient: false,
      shareTargetPickerAvailable: false,
      contextType: null,
      errorCode: 'LIFF_ID_MISSING',
      errorMessage: 'VITE_LINE_LIFF_ID 未設定',
    }
  }

  try {
    const liff = await ensureLiffReady()
    if (!liff) {
      return {
        liffIdConfigured: true,
        liffReady: false,
        isLoggedIn: false,
        isInClient: false,
        shareTargetPickerAvailable: false,
        contextType: null,
        errorCode: 'LIFF_INIT_FAILED',
        errorMessage: 'LIFF 初始化失敗',
      }
    }

    return {
      liffIdConfigured: true,
      liffReady: true,
      isLoggedIn: liff.isLoggedIn(),
      isInClient: liff.isInClient(),
      shareTargetPickerAvailable: safeSharePickerAvailable(liff),
      contextType: liff.getContext()?.type ?? null,
      errorCode: null,
      errorMessage: null,
    }
  } catch (err) {
    const errorMessage = err instanceof Error ? err.message : 'LIFF 狀態檢查發生未知錯誤'
    return {
      liffIdConfigured: true,
      liffReady: false,
      isLoggedIn: false,
      isInClient: false,
      shareTargetPickerAvailable: false,
      contextType: null,
      errorCode: 'LIFF_RUNTIME_STATUS_EXCEPTION',
      errorMessage,
    }
  }
}
