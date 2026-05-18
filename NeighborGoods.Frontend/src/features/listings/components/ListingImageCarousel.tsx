import { useCallback, useEffect, useRef, useState } from 'react'
import { ChevronLeft, ChevronRight } from 'lucide-react'

type ListingImageCarouselProps = {
  urls: string[]
  title: string
  imageClassName?: string
}

export const ListingImageCarousel = ({
  urls,
  title,
  imageClassName = 'aspect-[16/10] w-full object-cover lg:aspect-[4/3]',
}: ListingImageCarouselProps) => {
  const scrollRef = useRef<HTMLDivElement>(null)
  const [activeIndex, setActiveIndex] = useState(0)
  const total = urls.length
  const trackKey = urls.join('|')

  useEffect(() => {
    const el = scrollRef.current
    if (el) {
      el.scrollLeft = 0
    }
    setActiveIndex(0)
  }, [trackKey])

  const syncActiveFromScroll = useCallback(() => {
    const el = scrollRef.current
    if (!el || total <= 1) {
      return
    }
    const pageWidth = el.clientWidth
    if (pageWidth <= 0) {
      return
    }
    const next = Math.round(el.scrollLeft / pageWidth)
    setActiveIndex(Math.min(Math.max(0, next), total - 1))
  }, [total])

  useEffect(() => {
    const el = scrollRef.current
    if (!el) {
      return
    }

    el.addEventListener('scroll', syncActiveFromScroll, { passive: true })
    el.addEventListener('scrollend', syncActiveFromScroll)

    return () => {
      el.removeEventListener('scroll', syncActiveFromScroll)
      el.removeEventListener('scrollend', syncActiveFromScroll)
    }
  }, [syncActiveFromScroll, trackKey])

  const goBy = useCallback((direction: -1 | 1) => {
    const el = scrollRef.current
    if (!el) {
      return
    }
    const pageWidth = el.clientWidth
    const maxScroll = Math.max(0, el.scrollWidth - pageWidth)
    const delta = direction * pageWidth
    const nextLeft = Math.min(maxScroll, Math.max(0, el.scrollLeft + delta))
    el.scrollTo({ left: nextLeft, behavior: 'smooth' })
  }, [])

  const goToIndex = useCallback(
    (index: number) => {
      const el = scrollRef.current
      if (!el) {
        return
      }
      const pageWidth = el.clientWidth
      if (pageWidth <= 0) {
        return
      }
      const clamped = Math.min(Math.max(0, index), total - 1)
      el.scrollTo({ left: clamped * pageWidth, behavior: 'smooth' })
    },
    [total],
  )

  if (total === 0) {
    return null
  }

  return (
    <div className="relative">
      <div
        ref={scrollRef}
        role="region"
        aria-roledescription="carousel"
        aria-label="商品照片"
        className="flex snap-x snap-mandatory overflow-x-auto overscroll-x-contain scroll-smooth touch-pan-x [scrollbar-width:none] [-ms-overflow-style:none] [&::-webkit-scrollbar]:hidden"
      >
        {urls.map((url, index) => (
          <div key={`${url}-${index.toString()}`} className="w-full min-w-full shrink-0 snap-center snap-always">
            <img
              src={url}
              alt={`${title}（${(index + 1).toString()}/${total.toString()}）`}
              className={imageClassName}
              draggable={false}
              loading={index === 0 ? 'eager' : 'lazy'}
            />
          </div>
        ))}
      </div>

      {total > 1 ? (
        <>
          <p
            className="pointer-events-none absolute right-2 top-2 z-10 rounded-full bg-black/50 px-2.5 py-0.5 text-sm font-semibold tabular-nums text-white"
            aria-live="polite"
          >
            {activeIndex + 1} / {total}
          </p>
          <button
            type="button"
            aria-label="上一張"
            onClick={() => goBy(-1)}
            className="absolute left-1.5 top-1/2 z-10 flex size-9 -translate-y-1/2 items-center justify-center rounded-full bg-black/40 text-white shadow-sm backdrop-blur-[2px] transition hover:bg-black/55 md:left-2 md:size-10"
          >
            <ChevronLeft className="size-6 md:size-7" aria-hidden />
          </button>
          <button
            type="button"
            aria-label="下一張"
            onClick={() => goBy(1)}
            className="absolute right-1.5 top-1/2 z-10 flex size-9 -translate-y-1/2 items-center justify-center rounded-full bg-black/40 text-white shadow-sm backdrop-blur-[2px] transition hover:bg-black/55 md:right-2 md:size-10"
          >
            <ChevronRight className="size-6 md:size-7" aria-hidden />
          </button>
          <nav
            className="absolute bottom-2 left-0 right-0 z-10 flex justify-center gap-1.5 px-2"
            aria-label="照片位置"
          >
            {urls.map((_, index) => (
              <button
                key={index.toString()}
                type="button"
                aria-label={`第 ${(index + 1).toString()} 張`}
                aria-current={activeIndex === index ? true : undefined}
                onClick={() => goToIndex(index)}
                className={`size-2 rounded-full transition ${
                  activeIndex === index ? 'bg-white shadow' : 'bg-white/45 hover:bg-white/70'
                }`}
              />
            ))}
          </nav>
        </>
      ) : null}
    </div>
  )
}
