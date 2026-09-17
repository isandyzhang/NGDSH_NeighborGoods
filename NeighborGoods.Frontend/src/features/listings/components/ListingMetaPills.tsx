const PILL_BASE = 'inline-flex max-w-full items-center truncate rounded-full font-semibold tracking-wide'

const PILL_SIZE = {
  sm: 'px-2 py-0.5 text-[11px] leading-4',
  md: 'px-3 py-1 text-base md:text-lg',
} as const

/** 品況：越新顏色越深的鼠尾草綠 */
const CONDITION_TONES: Record<number, string> = {
  0: 'bg-[#B7D7C8] text-[#1B5241]',
  1: 'bg-[#C8E1D5] text-[#276353]',
  2: 'bg-[#D7EBE3] text-[#2F6B54]',
  3: 'bg-[#E4F1EB] text-[#3E7562]',
  4: 'bg-[#EDE8E0] text-[#6A5E52]',
}

const CONDITION_TONE_FALLBACK = CONDITION_TONES[2]

/** 社宅：暖橘色深淺，跟品況明顯分開 */
const RESIDENCE_TONES: Record<number, string> = {
  0: 'bg-[#E8E2DA] text-[#6B6158]',
  1: 'bg-[#F1D0BE] text-[#7E3D2C]',
  2: 'bg-[#F4DDB8] text-[#7E5420]',
  3: 'bg-[#E5D0C2] text-[#5F4336]',
}

const RESIDENCE_TONE_FALLBACK = 'bg-[#F3DCCB] text-[#8A4A32]'

type PillSize = keyof typeof PILL_SIZE

type Props = {
  conditionCode: number
  conditionName: string
  residenceCode: number
  residenceName: string
  size?: PillSize
}

export const getConditionPillClassName = (conditionCode: number, size: PillSize = 'sm') =>
  `${PILL_BASE} ${PILL_SIZE[size]} ${CONDITION_TONES[conditionCode] ?? CONDITION_TONE_FALLBACK}`

export const getResidencePillClassName = (residenceCode: number, size: PillSize = 'sm') =>
  `${PILL_BASE} ${PILL_SIZE[size]} ${RESIDENCE_TONES[residenceCode] ?? RESIDENCE_TONE_FALLBACK}`

export const ListingMetaPills = ({
  conditionCode,
  conditionName,
  residenceCode,
  residenceName,
  size = 'sm',
}: Props) => (
  <div className="flex flex-wrap items-center gap-1.5">
    {conditionName ? (
      <span className={getConditionPillClassName(conditionCode, size)}>{conditionName}</span>
    ) : null}
    {residenceName ? (
      <span className={getResidencePillClassName(residenceCode, size)}>{residenceName}</span>
    ) : null}
  </div>
)
