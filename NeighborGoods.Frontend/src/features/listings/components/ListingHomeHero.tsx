import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { Button, getButtonClassName } from '@/shared/ui/Button'

type Props = {
  unreadMessageCount: number
  onBrowseListings: () => void
}

export const ListingHomeHero = ({ unreadMessageCount, onBrowseListings }: Props) => (
  <section className="order-2 mb-0 flex min-h-[62vh] flex-col items-center justify-center space-y-3 text-center md:order-1 md:min-h-[75vh]">
    <p className="animate-fade-in text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
    <h1
      className="animate-fade-in inline-block text-6xl font-semibold leading-[1.03] text-text-main sm:text-7xl md:text-8xl"
      style={{ animationDelay: '160ms' }}
    >
      <span className="block">
        社宅<span className="marker-wipe">專屬</span>
      </span>
      <span className="block">二手交易平台</span>
    </h1>
    <div
      className="animate-fade-in mt-6 grid w-full max-w-[24rem] grid-cols-2 gap-3 md:flex md:max-w-none md:flex-wrap md:items-center md:justify-center md:gap-4"
      style={{ animationDelay: '780ms' }}
    >
      <motion.div
        initial={{ opacity: 0, y: 28, scale: 0.96 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        transition={{ duration: 0.7, delay: 0.75, ease: [0.22, 1, 0.36, 1] }}
      >
        <Button
          type="button"
          className="text-swap-trigger !h-[3.7rem] !w-full rounded-2xl !px-0 !py-0 !text-[1.45rem] !font-semibold shadow-soft md:!h-[4.6rem] md:!w-[16rem] md:!text-2xl"
          onClick={onBrowseListings}
        >
          <span className="btn-text-swap">
            <span className="btn-text-swap-primary">瀏覽商品</span>
            <span className="btn-text-swap-secondary">
              <span className="btn-text-icon" aria-hidden="true">
                ↓
              </span>
              立即瀏覽
            </span>
          </span>
        </Button>
      </motion.div>
      <motion.div
        initial={{ opacity: 0, y: 28, scale: 0.96 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        transition={{ duration: 0.7, delay: 0.84, ease: [0.22, 1, 0.36, 1] }}
      >
        <Link
          to="/listings/create"
          className={getButtonClassName({
            className:
              'inline-flex h-[3.7rem] w-full items-center justify-center rounded-2xl px-0 py-0 text-[1.45rem] font-semibold md:h-[4.6rem] md:w-[16rem] md:text-2xl',
          })}
        >
          刊登商品
        </Link>
      </motion.div>
      <motion.div
        className="col-span-2 md:col-span-1"
        initial={{ opacity: 0, y: 28, scale: 0.96 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        transition={{ duration: 0.7, delay: 0.96, ease: [0.22, 1, 0.36, 1] }}
      >
        <Link
          to="/messages"
          className={getButtonClassName({
            variant: 'secondary',
            className:
              'text-swap-trigger relative inline-flex h-[3.7rem] w-full items-center justify-center rounded-2xl px-0 py-0 text-[1.45rem] font-semibold md:h-[4.6rem] md:w-[16rem] md:text-2xl',
          })}
        >
          <span className="btn-text-swap">
            <span className="btn-text-swap-primary">我的訊息</span>
            <span className="btn-text-swap-secondary">
              <span className="btn-text-icon" aria-hidden="true">
                ✉
              </span>
              前往聊天室
            </span>
          </span>
          {unreadMessageCount > 0 ? (
            <span className="absolute right-2 top-2 inline-flex min-h-6 min-w-6 items-center justify-center rounded-full bg-[#D64545] px-1.5 text-lg font-bold leading-none text-white md:right-2.5 md:top-2.5 md:min-h-7 md:min-w-7 md:px-1.5 md:text-2xl">
              {unreadMessageCount > 99 ? '99+' : unreadMessageCount}
            </span>
          ) : null}
        </Link>
      </motion.div>
    </div>
  </section>
)
