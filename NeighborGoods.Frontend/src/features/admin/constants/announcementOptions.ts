export const ANNOUNCEMENT_SEVERITY_OPTIONS = [
  { value: 1, label: '資訊' },
  { value: 2, label: '注意' },
  { value: 3, label: '緊急' },
] as const

export const ANNOUNCEMENT_SCOPE_OPTIONS = [
  { value: 0, label: '全站' },
  { value: 1, label: '僅商品首頁' },
] as const

export const getSeverityLabel = (severity: number) =>
  ANNOUNCEMENT_SEVERITY_OPTIONS.find((option) => option.value === severity)?.label ?? `嚴重度 ${severity}`

export const getScopeLabel = (scope: number) =>
  ANNOUNCEMENT_SCOPE_OPTIONS.find((option) => option.value === scope)?.label ?? `範圍 ${scope}`

/** 跑馬燈前方 emoji，區分資訊 / 注意 / 緊急 */
export const ANNOUNCEMENT_SEVERITY_EMOJI: Record<number, string> = {
  1: '📢',
  2: '⚠️',
  3: '🚨',
}

export const getSeverityEmoji = (severity: number) =>
  ANNOUNCEMENT_SEVERITY_EMOJI[severity] ?? ANNOUNCEMENT_SEVERITY_EMOJI[1]
