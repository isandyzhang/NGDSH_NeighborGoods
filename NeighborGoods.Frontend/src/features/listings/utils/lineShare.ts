const LINE_SHARE_BASE_URL = 'https://social-plugins.line.me/lineit/share'
const LINE_SHARE_PREFIX = '各位好厝邊大家好！我要分享一個超棒的東西，如果有興趣請來網站看看喔！'
const LINE_TEXT_SHARE_BASE_URL = 'https://line.me/R/msg/text/?'
const LIFF_ID = import.meta.env.VITE_LINE_LIFF_ID as string | undefined
const FLEX_HERO_IMAGE_URL = `${window.location.origin}/logo.png`
const FLEX_ALT_TEXT_PREFIX = '我在NeighborGoods-社宅二手交易平台看到一個好物：'
const MAX_TITLE_LENGTH = 40
const MAX_META_LENGTH = 24

type ListingFlexPayload = {
  listingId: string
  listingTitle: string
  priceLabel?: string
  categoryName?: string
  conditionName?: string
}

type ShareListingOptions = ListingFlexPayload & {
  origin?: string
}

export type ShareListingResult = {
  usedLiffFlex: boolean
  usedFallbackUrlShare: boolean
  fallbackUrl?: string
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
  origin,
}: ListingFlexPayload & { origin?: string }) => {
  const listingUrl = buildListingUrl(listingId, origin)
  const title = trimText(listingTitle, MAX_TITLE_LENGTH, '好物分享')
  const category = trimText(categoryName, MAX_META_LENGTH, '未分類')
  const condition = trimText(conditionName, MAX_META_LENGTH, '未提供')
  const price = trimText(priceLabel, MAX_META_LENGTH, '歡迎查看商品詳情')

  return {
    type: 'flex' as const,
    altText: `${FLEX_ALT_TEXT_PREFIX}${title}`,
    contents: {
      type: 'bubble' as const,
      hero: {
        type: 'image' as const,
        url: FLEX_HERO_IMAGE_URL,
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
            text: title,
            weight: 'bold' as const,
            size: 'lg' as const,
            wrap: true,
          },
          {
            type: 'text' as const,
            text: `價格：${price}`,
            size: 'sm' as const,
            color: '#666666',
            wrap: true,
          },
          {
            type: 'text' as const,
            text: `分類：${category} ｜ 品況：${condition}`,
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
              label: '查看商品',
              uri: listingUrl,
            },
          },
        ],
      },
    },
  }
}

let liffReadyPromise: Promise<boolean> | null = null

const ensureLiffReady = async () => {
  if (!LIFF_ID?.trim()) {
    return null
  }

  const liffMod = await import('@line/liff')
  const liff = liffMod.default
  if (!liffReadyPromise) {
    liffReadyPromise = liff
      .init({ liffId: LIFF_ID.trim() })
      .then(() => true)
      .catch(() => false)
  }
  const ok = await liffReadyPromise
  if (!ok) {
    return null
  }
  return liff
}

const openLineUrlShareWindow = (shareUrl: string) => {
  window.open(shareUrl, '_blank', 'noopener,noreferrer')
}

export const shareListingToLine = async (options: ShareListingOptions): Promise<ShareListingResult> => {
  const fallbackUrl = buildLineTextShareUrl(options.listingId, options.listingTitle, options.origin)

  try {
    const liff = await ensureLiffReady()
    if (!liff || !liff.isInClient() || !liff.isApiAvailable('shareTargetPicker')) {
      openLineUrlShareWindow(fallbackUrl)
      return { usedLiffFlex: false, usedFallbackUrlShare: true, fallbackUrl }
    }

    const flexMessage = buildListingFlexMessage(options)
    const result = await liff.shareTargetPicker([flexMessage])
    if (!result) {
      openLineUrlShareWindow(fallbackUrl)
      return { usedLiffFlex: false, usedFallbackUrlShare: true, fallbackUrl }
    }

    return { usedLiffFlex: true, usedFallbackUrlShare: false }
  } catch {
    openLineUrlShareWindow(fallbackUrl)
    return { usedLiffFlex: false, usedFallbackUrlShare: true, fallbackUrl }
  }
}
