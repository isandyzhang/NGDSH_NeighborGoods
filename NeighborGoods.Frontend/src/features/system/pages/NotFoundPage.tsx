import { Link } from 'react-router-dom'
import { getButtonClassName } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'

export const NotFoundPage = () => {
  return (
    <main className="mx-auto flex w-full max-w-3xl items-center justify-center px-4 py-8">
      <Card className="w-full text-center">
        <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="mt-3 text-4xl font-semibold text-text-main sm:text-5xl">
          404<span className="marker-wipe"> 找不到頁面</span>
        </h1>
        <p className="mt-4 text-base text-text-subtle">你要前往的頁面不存在或已被移除。</p>
        <div className="mt-6">
          <Link
            to="/listings"
            className={getButtonClassName({
              className: 'inline-flex min-h-[3rem] items-center justify-center px-6 text-base font-semibold',
            })}
          >
            回到商品列表
          </Link>
        </div>
      </Card>
    </main>
  )
}
