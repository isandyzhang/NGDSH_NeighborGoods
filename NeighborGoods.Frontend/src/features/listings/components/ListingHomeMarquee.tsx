import { useRef } from 'react'
import { motion } from 'framer-motion'

const MARQUEE_NOTICE_TEXT =
  '交易安全提醒：請勿使用匯款或寄送包裹，本網站僅提供面交交易服務。請事先約定地點並準時赴約，面交時請雙方當場確認商品狀況與數量；若為高價物品，建議先交換其他聯絡方式。本站不介入且不負責處理交易糾紛。'

const supportsDesktopHover = () => window.matchMedia('(hover: hover) and (pointer: fine)').matches

const renderMarqueeText = (text: string, keyPrefix: string) => (
  <span className="marquee-divider-item">
    {Array.from(text).map((char, index) => (
      <span key={`${keyPrefix}-${index}`} className="marquee-char" aria-hidden="true">
        {char === ' ' ? '\u00A0' : char}
      </span>
    ))}
  </span>
)

export const ListingHomeMarquee = () => {
  const marqueeRef = useRef<HTMLElement | null>(null)
  const pointerXRef = useRef<number | null>(null)

  const updateMarqueeFisheye = (clientX: number) => {
    const container = marqueeRef.current
    if (!container) {
      return
    }

    const radius = 190
    const maxScaleBoost = 0.55
    const maxGapEm = 0.14
    const chars = container.querySelectorAll<HTMLElement>('.marquee-char')
    chars.forEach((char) => {
      const rect = char.getBoundingClientRect()
      const centerX = rect.left + rect.width / 2
      const distance = Math.abs(centerX - clientX)
      const normalized = Math.max(0, 1 - distance / radius)
      const scale = 1 + normalized * maxScaleBoost
      const gap = normalized * maxGapEm
      char.style.setProperty('--char-scale', scale.toFixed(3))
      char.style.setProperty('--char-gap', `${gap.toFixed(3)}em`)
    })
  }

  const handleMouseEnter = (event: React.MouseEvent<HTMLElement>) => {
    if (!supportsDesktopHover()) {
      return
    }
    pointerXRef.current = event.clientX
    updateMarqueeFisheye(event.clientX)
  }

  const handleMouseMove = (event: React.MouseEvent<HTMLElement>) => {
    pointerXRef.current = event.clientX
    if (!supportsDesktopHover()) {
      return
    }
    updateMarqueeFisheye(event.clientX)
  }

  const handleMouseLeave = () => {
    pointerXRef.current = null
    const container = marqueeRef.current
    if (!container) {
      return
    }
    const chars = container.querySelectorAll<HTMLElement>('.marquee-char')
    chars.forEach((char) => {
      char.style.setProperty('--char-scale', '1')
      char.style.setProperty('--char-gap', '0em')
    })
  }

  return (
    <motion.section
      ref={marqueeRef}
      className="marquee-divider order-1 h-auto min-h-[2.8rem] md:order-2 md:h-[75vh]"
      aria-label="交易安全提醒"
      initial={{ opacity: 0, y: 90 }}
      whileInView={{ opacity: 1, y: 0 }}
      viewport={{ once: false, amount: 0.42 }}
      transition={{ duration: 0.85, ease: [0.22, 1, 0.36, 1] }}
      onMouseEnter={handleMouseEnter}
      onMouseMove={handleMouseMove}
      onMouseLeave={handleMouseLeave}
    >
      <div className="marquee-divider-track">
        {renderMarqueeText(MARQUEE_NOTICE_TEXT, 'notice-a')}
        <span aria-hidden="true">{renderMarqueeText(MARQUEE_NOTICE_TEXT, 'notice-b')}</span>
      </div>
    </motion.section>
  )
}
