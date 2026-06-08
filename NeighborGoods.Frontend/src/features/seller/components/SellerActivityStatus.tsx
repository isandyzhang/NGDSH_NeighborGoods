export type SellerLoginActivityLevel = 'recent' | 'today' | 'week' | 'inactive' | 'stale' | 'unknown'

type SellerActivityStatusProps = {
  loginActivityLabel: string
  loginActivityLevel: SellerLoginActivityLevel | string
  typicalReplyMinutes?: number | null
  quickResponder?: boolean
  className?: string
}

const levelDotClass: Record<string, string> = {
  recent: 'bg-emerald-500',
  today: 'bg-emerald-500',
  week: 'bg-amber-400',
  inactive: 'bg-orange-400',
  stale: 'bg-rose-400',
  unknown: 'bg-text-muted',
}

const formatTypicalReply = (minutes: number): string => {
  if (minutes < 60) {
    return `通常 ${minutes} 分鐘內回覆`
  }

  const hours = Math.max(1, Math.round(minutes / 60))
  return `通常 ${hours} 小時內回覆`
}

export const SellerActivityStatus = ({
  loginActivityLabel,
  loginActivityLevel,
  typicalReplyMinutes,
  quickResponder = false,
  className = '',
}: SellerActivityStatusProps) => {
  const dotClass = levelDotClass[loginActivityLevel] ?? levelDotClass.unknown
  const replyHint =
    quickResponder && typicalReplyMinutes && typicalReplyMinutes > 0
      ? formatTypicalReply(typicalReplyMinutes)
      : null

  return (
    <p className={`flex items-center justify-center gap-2 text-sm font-medium text-text-main md:justify-start md:text-base ${className}`.trim()}>
      <span className={`inline-block h-2.5 w-2.5 shrink-0 rounded-full ${dotClass}`} aria-hidden="true" />
      <span>{loginActivityLabel}</span>
      {replyHint ? (
        <>
          <span className="text-text-muted" aria-hidden="true">
            ·
          </span>
          <span className="text-text-subtle">{replyHint}</span>
        </>
      ) : null}
    </p>
  )
}
