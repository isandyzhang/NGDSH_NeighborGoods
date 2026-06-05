import { DotLottieReact } from '@lottiefiles/dotlottie-react'
import { AnimatePresence, motion } from 'framer-motion'
import { useCallback, useEffect, useMemo, useState, type PropsWithChildren } from 'react'
import { pingApi } from '@/shared/api/warmup'
import { getButtonClassName } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'

const WARMUP_TIMEOUT_MESSAGE = '啟動時間比預期久一點，請再試一次'
const WARMUP_RETRY_LABEL = '再試一次'
const LINE_OFFICIAL_ACCOUNT_URL = 'https://lin.ee/6ZqrGei'
const LINE_ICON = new URL('../../../png/line_icon.png', import.meta.url).href
const WARMUP_PLANE_LOTTIE = new URL('../../../lottie/Send Animated Icon.lottie', import.meta.url).href

const CAROUSEL_MOTION = {
  initial: { opacity: 0, y: 12 },
  animate: { opacity: 1, y: 0 },
  exit: { opacity: 0, y: -10 },
  transition: { duration: 0.42, ease: [0.22, 1, 0.36, 1] as const },
}

const POLL_INTERVAL_MS = 4_000
const MAX_WAIT_MS = 180_000
const KNOWLEDGE_ROTATE_INTERVAL_MS = 7_000

type GatePhase = 'warming' | 'ready' | 'timedOut'

const delay = (ms: number, signal: AbortSignal): Promise<void> =>
  new Promise((resolve, reject) => {
    const timer = window.setTimeout(resolve, ms)
    signal.addEventListener(
      'abort',
      () => {
        window.clearTimeout(timer)
        reject(new DOMException('Aborted', 'AbortError'))
      },
      { once: true },
    )
  })

export const ApiWarmupGate = ({ children }: PropsWithChildren) => {
  const [phase, setPhase] = useState<GatePhase>('warming')
  const [retryKey, setRetryKey] = useState(0)
  const [knowledgeIndex, setKnowledgeIndex] = useState(0)

  const knowledgeItems = useMemo(
    () => [
      '大概只要去裝杯水的時間，請稍候…',
      '你知道本平台是青創夥伴創立的嗎？\n因為無償所以才會需要啟動時間 XD',
      '社宅也會不定時準備二手實體市集，大家可以踴躍報名！',
      '大家如果有問題，可以直接加入 LINE 官方帳號私訊管理員。',
      '曾曾見舊物換新緣，玉價無欺兩意全。\n堂前百貨循環轉，市井交易勝有田。',
      '歡迎把你的商品漂亮的分享出去！',
      '大家交易要自己注意，只能面交！一手交錢一手交貨！',
    ],
    [],
  )

  const handleRetry = useCallback(() => {
    setKnowledgeIndex(0)
    setPhase('warming')
    setRetryKey((key) => key + 1)
  }, [])

  useEffect(() => {
    if (phase !== 'warming') {
      return
    }

    const timer = window.setInterval(() => {
      setKnowledgeIndex((prev) => (prev + 1) % knowledgeItems.length)
    }, KNOWLEDGE_ROTATE_INTERVAL_MS)

    return () => {
      window.clearInterval(timer)
    }
  }, [knowledgeItems.length, phase])

  useEffect(() => {
    if (phase !== 'warming') {
      return
    }

    let disposed = false
    const controller = new AbortController()
    const startedAt = Date.now()

    const runWarmup = async () => {
      while (!disposed && !controller.signal.aborted) {
        if (Date.now() - startedAt >= MAX_WAIT_MS) {
          if (!disposed) {
            setPhase('timedOut')
          }
          return
        }

        const ok = await pingApi()
        if (disposed) {
          return
        }
        if (ok) {
          setPhase('ready')
          return
        }

        try {
          await delay(POLL_INTERVAL_MS, controller.signal)
        } catch {
          return
        }
      }
    }

    void runWarmup()

    return () => {
      disposed = true
      controller.abort()
    }
  }, [phase, retryKey])

  if (phase === 'ready') {
    return children
  }

  const carouselMessage = knowledgeItems[knowledgeIndex]

  return (
    <main className="mx-auto flex min-h-[60vh] w-full max-w-3xl items-center justify-center px-4 py-8">
      <Card className="flex w-full flex-col text-center">
        <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="mt-3 text-4xl font-semibold leading-tight text-text-main sm:text-5xl">
          服務正在<span className="marker-wipe">啟動中</span>
        </h1>
        <div className="mt-3 flex justify-center" role="status" aria-label="服務啟動中">
          <DotLottieReact src={WARMUP_PLANE_LOTTIE} autoplay loop style={{ width: '92px', height: '92px' }} />
        </div>
        <div className="relative mt-6 min-h-[4.5rem] overflow-hidden px-2">
          <AnimatePresence mode="wait">
            {phase === 'timedOut' ? (
              <motion.p
                key="timeout"
                className="text-base whitespace-pre-line text-text-subtle"
                {...CAROUSEL_MOTION}
              >
                {WARMUP_TIMEOUT_MESSAGE}
              </motion.p>
            ) : (
              <motion.p
                key={knowledgeIndex}
                className="text-base whitespace-pre-line text-text-subtle"
                {...CAROUSEL_MOTION}
              >
                {carouselMessage}
              </motion.p>
            )}
          </AnimatePresence>
        </div>
        {phase === 'timedOut' ? (
          <div className="mt-6 flex items-center justify-center">
            <button
              type="button"
              onClick={handleRetry}
              className={getButtonClassName({
                className: 'inline-flex min-h-[3rem] items-center justify-center px-6 text-base font-semibold',
              })}
            >
              {WARMUP_RETRY_LABEL}
            </button>
          </div>
        ) : null}
        <footer className="mt-8 border-t border-border pt-4">
          <a
            href={LINE_OFFICIAL_ACCOUNT_URL}
            target="_blank"
            rel="noreferrer"
            className="mx-auto inline-flex h-12 w-full max-w-sm items-center justify-center gap-3 rounded-xl bg-[#06C755] px-4 text-base font-semibold text-white transition hover:bg-[#05b64d]"
          >
            <span className="inline-flex h-6 w-6 items-center justify-center rounded bg-white/20">
              <img src={LINE_ICON} alt="" className="h-4 w-4 object-contain" aria-hidden="true" />
            </span>
            <span className="tracking-[0.02em]">加入官方帳號並私訊管理員</span>
          </a>
        </footer>
      </Card>
    </main>
  )
}
