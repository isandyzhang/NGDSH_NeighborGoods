export const formatListingPrice = (item: { isFree: boolean; price: number }) => {
  if (item.isFree) {
    return '免費'
  }

  return `NT$ ${item.price.toLocaleString()}`
}

export const formatCountdown = (seconds: number) => {
  const normalized = Math.max(0, Math.floor(seconds))
  const hours = Math.floor(normalized / 3600)
  const minutes = Math.floor((normalized % 3600) / 60)
  const remainingSeconds = normalized % 60
  return [hours, minutes, remainingSeconds].map((value) => value.toString().padStart(2, '0')).join(':')
}

export const parseApiDateToMs = (value: string | null | undefined) => {
  if (!value) {
    return null
  }

  const hasTimezone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value)
  const normalized = hasTimezone ? value : `${value}Z`
  const parsed = Date.parse(normalized)
  return Number.isNaN(parsed) ? null : parsed
}

export const getPendingRemainingSeconds = (
  expireAt: string | null | undefined,
  remainingFromServer: number | null | undefined,
  nowMs = Date.now(),
) => {
  const expireAtMs = parseApiDateToMs(expireAt)
  const remainingFromNow = expireAtMs == null ? null : Math.max(0, Math.floor((expireAtMs - nowMs) / 1000))
  return remainingFromNow ?? (remainingFromServer == null ? null : Math.max(0, remainingFromServer))
}
