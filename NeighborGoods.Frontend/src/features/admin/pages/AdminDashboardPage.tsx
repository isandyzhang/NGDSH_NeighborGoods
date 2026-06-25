import { useCallback, useEffect, useState } from 'react'
import { adminApi, type AdminDashboard } from '@/features/admin/api/adminApi'
import { ApiClientError } from '@/shared/types/api'
import { Card } from '@/shared/ui/Card'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

const formatNum = (value: number) => value.toLocaleString('zh-TW')
const formatPercent = (count: number, total: number) => (total <= 0 ? '0.0%' : `${((count / total) * 100).toFixed(1)}%`)

const StatCard = ({ title, value, sub }: { title: string; value: number; sub?: string }) => (
  <Card>
    <p className="text-sm text-text-subtle">{title}</p>
    <p className="mt-2 text-3xl font-bold text-text-main">{formatNum(value)}</p>
    {sub ? <p className="mt-1 text-xs text-text-muted">{sub}</p> : null}
  </Card>
)

export const AdminDashboardPage = () => {
  const [dashboard, setDashboard] = useState<AdminDashboard | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      setDashboard(await adminApi.getDashboard())
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '讀取後台首頁失敗')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  if (loading) {
    return (
      <div className="space-y-4">
        <PageSkeleton className="h-28" />
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
          <PageSkeleton className="h-28" />
          <PageSkeleton className="h-28" />
          <PageSkeleton className="h-28" />
          <PageSkeleton className="h-28" />
        </div>
      </div>
    )
  }

  if (error || !dashboard) {
    return <ErrorState description={error ?? '讀取後台首頁失敗'} onRetry={() => void load()} />
  }

  const { kpi } = dashboard
  return (
    <div className="space-y-6">
      <section className="space-y-3">
        <h2 className="text-xl font-semibold text-text-main">商品統計</h2>
        <StatCard title="總商品數量" value={kpi.totalListings} />
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
          <StatCard title="上架中商品" value={kpi.activeListings} sub={`七天內新增上架 ${formatNum(kpi.activeListingsLast7Days)}`} />
          <StatCard title="已售出商品" value={kpi.soldListings} sub={`七天內已售出 ${formatNum(kpi.soldListingsLast7Days)}`} />
          <StatCard title="已贈與商品" value={kpi.donatedListings} sub={`七天內已贈與 ${formatNum(kpi.donatedListingsLast7Days)}`} />
          <StatCard
            title="已易物商品"
            value={kpi.givenOrTradedListings}
            sub={`七天內已易物 ${formatNum(kpi.givenOrTradedListingsLast7Days)}`}
          />
        </div>
      </section>

      <section className="space-y-3">
        <h2 className="text-xl font-semibold text-text-main">會員統計</h2>
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          <StatCard title="會員總人數" value={kpi.totalMembers} />
          <StatCard
            title="帳號密碼登入者"
            value={kpi.passwordLoginMembers}
            sub={`佔比 ${formatPercent(kpi.passwordLoginMembers, kpi.totalMembers)}`}
          />
          <StatCard title="LINE 登入者" value={kpi.lineLoginMembers} sub={`佔比 ${formatPercent(kpi.lineLoginMembers, kpi.totalMembers)}`} />
          <StatCard title="Email 綁定數量" value={kpi.emailBoundMembers} sub={`佔比 ${formatPercent(kpi.emailBoundMembers, kpi.totalMembers)}`} />
          <StatCard
            title="LINE 官方帳號綁定"
            value={kpi.lineOfficialBoundMembers}
            sub={`佔比 ${formatPercent(kpi.lineOfficialBoundMembers, kpi.totalMembers)}`}
          />
          <StatCard title="24 小時有上線人數" value={kpi.activeMembers24h} sub={`佔比 ${formatPercent(kpi.activeMembers24h, kpi.totalMembers)}`} />
          <StatCard title="7 天內有上線人數" value={kpi.activeMembers7d} sub={`佔比 ${formatPercent(kpi.activeMembers7d, kpi.totalMembers)}`} />
          <StatCard
            title="30 天內有上線人數"
            value={kpi.activeMembers30d}
            sub={`佔比 ${formatPercent(kpi.activeMembers30d, kpi.totalMembers)}`}
          />
          <StatCard title="24h Email 寄出人數" value={kpi.emailedMembers24h} sub={`佔比 ${formatPercent(kpi.emailedMembers24h, kpi.totalMembers)}`} />
          <StatCard title="7d Email 寄出人數" value={kpi.emailedMembers7d} sub={`佔比 ${formatPercent(kpi.emailedMembers7d, kpi.totalMembers)}`} />
          <StatCard
            title="30d Email 寄出人數"
            value={kpi.emailedMembers30d}
            sub={`佔比 ${formatPercent(kpi.emailedMembers30d, kpi.totalMembers)}`}
          />
          <StatCard
            title="24h LINE 通知人數"
            value={kpi.lineNotifiedMembers24h}
            sub={`佔比 ${formatPercent(kpi.lineNotifiedMembers24h, kpi.totalMembers)}`}
          />
          <StatCard
            title="7d LINE 通知人數"
            value={kpi.lineNotifiedMembers7d}
            sub={`佔比 ${formatPercent(kpi.lineNotifiedMembers7d, kpi.totalMembers)}`}
          />
          <StatCard
            title="30d LINE 通知人數"
            value={kpi.lineNotifiedMembers30d}
            sub={`佔比 ${formatPercent(kpi.lineNotifiedMembers30d, kpi.totalMembers)}`}
          />
        </div>
      </section>
    </div>
  )
}
