/** 與後端 ListingStatus 整數語意一致 */
export const LISTING_STATUS = {
  Active: 0,
  Reserved: 1,
  Sold: 2,
  Donated: 3,
  Inactive: 4,
  GivenOrTraded: 5,
} as const

export const LISTING_STATUS_LABEL: Record<number, string> = {
  [LISTING_STATUS.Active]: '上架中',
  [LISTING_STATUS.Reserved]: '保留中',
  [LISTING_STATUS.Sold]: '已售出',
  [LISTING_STATUS.Donated]: '已捐贈',
  [LISTING_STATUS.Inactive]: '已下架',
  [LISTING_STATUS.GivenOrTraded]: '已易物',
}

export const getListingStatusLabel = (statusCode: number) =>
  LISTING_STATUS_LABEL[statusCode] ?? `狀態 ${statusCode}`

export const isTerminalListingStatus = (statusCode: number) =>
  statusCode === LISTING_STATUS.Sold ||
  statusCode === LISTING_STATUS.Donated ||
  statusCode === LISTING_STATUS.GivenOrTraded

export const canEditListing = (statusCode: number) =>
  statusCode === LISTING_STATUS.Active || statusCode === LISTING_STATUS.Inactive

export type CanPurchaseListingOptions = {
  hasPendingPurchaseRequest?: boolean
  hasInProgressTrade?: boolean
}

export const canPurchaseListing = (
  statusCode: number,
  options: CanPurchaseListingOptions = {},
) => {
  if (statusCode !== LISTING_STATUS.Active) {
    return false
  }
  if (options.hasPendingPurchaseRequest) {
    return false
  }
  if (options.hasInProgressTrade) {
    return false
  }
  return true
}

export type ListingDetailOverlay = 'pending' | 'reserved' | null

export const getListingDetailOverlay = (
  statusCode: number,
  hasPendingPurchaseRequest: boolean,
): ListingDetailOverlay => {
  if (hasPendingPurchaseRequest) {
    return 'pending'
  }
  if (statusCode === LISTING_STATUS.Reserved) {
    return 'reserved'
  }
  return null
}

export const shouldShowUnavailableBanner = (statusCode: number) =>
  statusCode !== LISTING_STATUS.Active

export const getUnavailableBannerMessage = (statusCode: number): string => {
  switch (statusCode) {
    case LISTING_STATUS.Reserved:
      return '此商品目前保留中，暫時無法購買。'
    case LISTING_STATUS.Inactive:
      return '此商品已下架，無法購買。'
    case LISTING_STATUS.Sold:
      return '此商品已售出，無法購買。'
    case LISTING_STATUS.Donated:
      return '此商品已捐贈，無法購買。'
    case LISTING_STATUS.GivenOrTraded:
      return '此商品已易物，無法購買。'
    default:
      return '此商品目前無法購買。'
  }
}

export const canShareListing = (statusCode: number) => statusCode === LISTING_STATUS.Active
