import { useCallback, useEffect, useState } from 'react'
import {
  adminApi,
  type AdminDashboard,
  type AdminLineTestPushResult,
  type AdminLineTransactionGroup,
} from '@/features/admin/api/adminApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

const formatNum = (value: number) => value.toLocaleString('zh-TW')
const formatPercent = (count: number, total: number) => (total <= 0 ? '0.0%' : `${((count / total) * 100).toFixed(1)}%`)
const formatDateTime = (value: string) => {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }
  return date.toLocaleString('zh-TW')
}

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
  const [lineTestSending, setLineTestSending] = useState(false)
  const [lineTestResult, setLineTestResult] = useState<AdminLineTestPushResult | null>(null)
  const [lineTestError, setLineTestError] = useState<string | null>(null)
  const [transactionGroup, setTransactionGroup] = useState<AdminLineTransactionGroup | null>(null)
  const [transactionGroupError, setTransactionGroupError] = useState<string | null>(null)
  const [transactionGroupBusyId, setTransactionGroupBusyId] = useState<string | null>(null)
  const [copyNotice, setCopyNotice] = useState<string | null>(null)

  const loadTransactionGroup = useCallback(async () => {
    setTransactionGroupError(null)
    try {
      setTransactionGroup(await adminApi.getLineTransactionGroup())
    } catch (err) {
      setTransactionGroupError(err instanceof ApiClientError ? err.message : '讀取交易群組狀態失敗')
    }
  }, [])

  const load = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [dash] = await Promise.all([adminApi.getDashboard(), loadTransactionGroup()])
      setDashboard(dash)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '讀取後台首頁失敗')
    } finally {
      setLoading(false)
    }
  }, [loadTransactionGroup])

  const handleSendLineTestPush = useCallback(async () => {
    setLineTestSending(true)
    setLineTestError(null)
    setLineTestResult(null)
    try {
      setLineTestResult(await adminApi.sendLineTestPush())
    } catch (err) {
      setLineTestError(err instanceof ApiClientError ? err.message : 'LINE 測試推播失敗')
    } finally {
      setLineTestSending(false)
    }
  }, [])

  const handleCopyGroupId = useCallback(async (groupId: string) => {
    try {
      await navigator.clipboard.writeText(groupId)
      setCopyNotice('已複製 groupId')
    } catch {
      setCopyNotice('複製失敗，請手動選取')
    }
  }, [])

  const handleActivateGroup = useCallback(
    async (id: string) => {
      setTransactionGroupBusyId(id)
      setTransactionGroupError(null)
      try {
        setTransactionGroup(await adminApi.activateLineTransactionGroup(id))
      } catch (err) {
        setTransactionGroupError(err instanceof ApiClientError ? err.message : '設為交易群失敗')
      } finally {
        setTransactionGroupBusyId(null)
      }
    },
    [],
  )

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
  const activeGroup = transactionGroup?.active ?? null
  const inactiveGroups = transactionGroup?.inactive ?? []

  return (
    <div className="space-y-6">
      <section className="space-y-3">
        <h2 className="text-xl font-semibold text-text-main">LINE 推播診斷</h2>
        <Card>
          <p className="text-sm text-text-subtle">
            立即對「目前登入的管理員本人」發送一則 sample Flex 商品卡。收到＝token 正常；收不到＝token／綁定問題（可排除排程）。
          </p>
          <div className="mt-4">
            <Button type="button" disabled={lineTestSending} onClick={() => void handleSendLineTestPush()}>
              {lineTestSending ? '發送中…' : '發送 sample Flex 給自己'}
            </Button>
          </div>
          {lineTestError ? <p className="mt-3 text-sm text-red-600">{lineTestError}</p> : null}
          {lineTestResult ? (
            <div className="mt-3 space-y-1 text-sm text-text-main">
              <p className={lineTestResult.success ? 'text-emerald-700' : 'text-amber-700'}>{lineTestResult.message}</p>
              <p className="text-text-muted">
                code={lineTestResult.code}
                {' · '}
                token={lineTestResult.channelAccessTokenConfigured ? '已設定' : '未設定'}
                {' · '}
                綁定={lineTestResult.lineBound ? `是（${lineTestResult.lineMessagingApiUserIdSuffix ?? '-'}）` : '否'}
                {lineTestResult.httpStatusCode != null ? ` · HTTP ${lineTestResult.httpStatusCode}` : ''}
              </p>
              {lineTestResult.providerResponse ? (
                <pre className="mt-2 overflow-x-auto rounded-lg bg-surface-2 p-3 text-xs text-text-muted">
                  {lineTestResult.providerResponse}
                </pre>
              ) : null}
            </div>
          ) : null}

          <div className="mt-6 border-t border-border pt-4">
            <h3 className="text-base font-semibold text-text-main">交易群組</h3>
            {transactionGroupError ? <p className="mt-2 text-sm text-red-600">{transactionGroupError}</p> : null}
            {activeGroup ? (
              <div className="mt-3 space-y-2 text-sm text-text-main">
                <p className="text-emerald-700">狀態：已綁定</p>
                <p className="break-all font-mono text-xs sm:text-sm">groupId={activeGroup.groupId}</p>
                <p className="text-text-muted">加入時間：{formatDateTime(activeGroup.joinedAt)}</p>
                <div className="flex flex-wrap gap-2">
                  <Button type="button" variant="secondary" onClick={() => void handleCopyGroupId(activeGroup.groupId)}>
                    複製 groupId
                  </Button>
                  <Button type="button" variant="secondary" onClick={() => void loadTransactionGroup()}>
                    重新整理
                  </Button>
                </div>
                {copyNotice ? <p className="text-xs text-text-muted">{copyNotice}</p> : null}
              </div>
            ) : (
              <div className="mt-3 space-y-2 text-sm text-text-main">
                <p className="text-amber-700">狀態：尚未綁定</p>
                <ol className="list-decimal space-y-1 pl-5 text-text-subtle">
                  <li>在交易群把官方帳號加進去（LINE 會送 join，自動存 groupId）。</li>
                  <li>若官方帳號本來就在群裡，請在群內 @ 官方帳號一次補登記。</li>
                </ol>
                <Button type="button" variant="secondary" onClick={() => void loadTransactionGroup()}>
                  重新整理
                </Button>
              </div>
            )}

            {inactiveGroups.length > 0 ? (
              <div className="mt-4 space-y-2">
                <p className="text-sm font-medium text-text-main">其他已記錄的群組</p>
                <ul className="space-y-2">
                  {inactiveGroups.map((group) => (
                    <li
                      key={group.id}
                      className="flex flex-col gap-2 rounded-lg bg-surface-2 p-3 text-sm sm:flex-row sm:items-center sm:justify-between"
                    >
                      <div className="min-w-0">
                        <p className="break-all font-mono text-xs sm:text-sm">{group.groupId}</p>
                        <p className="text-xs text-text-muted">記錄時間：{formatDateTime(group.joinedAt)}</p>
                      </div>
                      <Button
                        type="button"
                        disabled={transactionGroupBusyId === group.id}
                        onClick={() => void handleActivateGroup(group.id)}
                      >
                        {transactionGroupBusyId === group.id ? '設定中…' : '設為交易群'}
                      </Button>
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
          </div>
        </Card>
      </section>

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
