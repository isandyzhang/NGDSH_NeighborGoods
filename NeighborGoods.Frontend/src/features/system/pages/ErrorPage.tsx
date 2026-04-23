import { Link, useLocation } from 'react-router-dom'
import { getButtonClassName } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'

type ErrorState = {
  message?: string
}

export const ErrorPage = () => {
  const location = useLocation()
  const state = (location.state as ErrorState | null) ?? null
  const message = state?.message ?? '系統暫時無法完成你的請求，請稍後再試。'

  return (
    <main className="mx-auto flex w-full max-w-3xl items-center justify-center px-4 py-8">
      <Card className="w-full text-center">
        <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="mt-3 text-4xl font-semibold text-text-main sm:text-5xl">
          系統<span className="marker-wipe">錯誤</span>
        </h1>
        <p className="mt-4 text-base text-danger">{message}</p>
        <div className="mt-6 flex items-center justify-center gap-2">
          <Link
            to="/listings"
            className={getButtonClassName({
              className: 'inline-flex min-h-[3rem] items-center justify-center px-6 text-base font-semibold',
            })}
          >
            回到商品列表
          </Link>
          <Link
            to="/contact-admin"
            className={getButtonClassName({
              variant: 'secondary',
              className: 'inline-flex min-h-[3rem] items-center justify-center px-6 text-base font-semibold',
            })}
          >
            聯絡管理員
          </Link>
        </div>
      </Card>
    </main>
  )
}
