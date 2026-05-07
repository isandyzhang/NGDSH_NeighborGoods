import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { adminApi, type AdminDashboard } from '@/features/admin/api/adminApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

const formatDateTime = (value: string) =>
  new Date(value).toLocaleString('zh-TW', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })

const formatPrice = (price: number, isFree: boolean) => (isFree ? '免費' : `NT$ ${Math.round(price).toLocaleString('zh-TW')}`)

export const AdminHomePage = () => {
  const [dashboard, setDashboard] = useState<AdminDashboard | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const loadDashboard = useCallback(async () => {
    const data = await adminApi.getDashboard()
    setDashboard(data)
  }, [])

  useEffect(() => {
    let disposed = false
    setLoading(true)
    setError(null)

    void loadDashboard()
      .catch((err: unknown) => {
        if (!disposed) {
          setError(err instanceof ApiClientError ? err.message : '讀取後台首頁失敗')
        }
      })
      .finally(() => {
        if (!disposed) {
          setLoading(false)
        }
      })

    return () => {
      disposed = true
    }
  }, [loadDashboard])

  if (loading) {
    return (
      <div className="space-y-4">
        <PageSkeleton className="h-32" />
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <PageSkeleton className="h-28" />
          <PageSkeleton className="h-28" />
          <PageSkeleton className="h-28" />
          <PageSkeleton className="h-28" />
        </div>
        <PageSkeleton className="h-72" />
      </div>
    )
  }

  if (error || !dashboard) {
    return <ErrorState description={error ?? '讀取後台首頁失敗'} onRetry={() => void loadDashboard()} />
  }

  const kpiCards = [
    { label: '總商品數', value: dashboard.kpi.totalListings },
    { label: '上架中商品', value: dashboard.kpi.activeListings },
    { label: '已完成交易', value: dashboard.kpi.completedListings },
    { label: '待處理事項', value: dashboard.kpi.pendingTopSubmissions + dashboard.kpi.unreadAdminMessages },
  ]

  return (
    <div className="space-y-6">
      <Card className="border-[#f2d59a] bg-[#fff7e0]">
        <p className="text-sm text-text-subtle">系統提醒</p>
        <p className="mt-1 text-lg font-semibold text-text-main">
          尚有 {dashboard.kpi.pendingTopSubmissions} 筆置頂投稿待審、{dashboard.kpi.unreadAdminMessages} 則管理訊息未讀。
        </p>
      </Card>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {kpiCards.map((item) => (
          <Card key={item.label}>
            <p className="text-sm text-text-subtle">{item.label}</p>
            <p className="mt-2 text-4xl font-bold text-text-main">{item.value.toLocaleString('zh-TW')}</p>
          </Card>
        ))}
      </div>

      <Card>
        <p className="text-sm font-semibold uppercase tracking-[0.1em] text-text-subtle">快捷動作</p>
        <div className="mt-3 flex flex-wrap gap-2">
          <Link to="/my-listings">
            <Button type="button" variant="secondary">
              商品管理
            </Button>
          </Link>
          <Link to="/messages">
            <Button type="button" variant="secondary">
              訊息管理
            </Button>
          </Link>
          <Link to="/contact-admin">
            <Button type="button" variant="secondary">
              聯絡入口
            </Button>
          </Link>
        </div>
      </Card>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card>
          <div className="mb-3 flex items-center justify-between">
            <h2 className="text-xl font-semibold text-text-main">最新商品</h2>
            <Link to="/listings" className="text-sm text-text-subtle hover:text-text-main">
              查看全部
            </Link>
          </div>
          <div className="space-y-2">
            {dashboard.latestListings.length === 0 ? (
              <p className="text-sm text-text-subtle">暫無資料</p>
            ) : (
              dashboard.latestListings.map((item) => (
                <div key={item.id} className="rounded-xl border border-border bg-surface-2 p-3">
                  <p className="font-semibold text-text-main">{item.title}</p>
                  <p className="mt-1 text-sm text-text-subtle">
                    {item.sellerDisplayName}・{formatPrice(item.price, item.isFree)}・{formatDateTime(item.createdAt)}
                  </p>
                </div>
              ))
            )}
          </div>
        </Card>

        <Card>
          <div className="mb-3 flex items-center justify-between">
            <h2 className="text-xl font-semibold text-text-main">最新管理訊息</h2>
            <Link to="/contact-admin" className="text-sm text-text-subtle hover:text-text-main">
              查看入口
            </Link>
          </div>
          <div className="space-y-2">
            {dashboard.latestMessages.length === 0 ? (
              <p className="text-sm text-text-subtle">暫無資料</p>
            ) : (
              dashboard.latestMessages.map((item) => (
                <div key={item.id} className="rounded-xl border border-border bg-surface-2 p-3">
                  <p className="line-clamp-1 font-semibold text-text-main">{item.content}</p>
                  <p className="mt-1 text-sm text-text-subtle">
                    {item.senderDisplayName}・{item.isRead ? '已讀' : '未讀'}・{formatDateTime(item.createdAt)}
                  </p>
                </div>
              ))
            )}
          </div>
        </Card>
      </div>
    </div>
  )
}
