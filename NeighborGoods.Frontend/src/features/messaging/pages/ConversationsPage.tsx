import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { messagingApi, type ConversationItem } from '@/features/messaging/api/messagingApi'
import { ApiClientError } from '@/shared/types/api'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'

const formatDateTime = (value: string | null) => {
  if (!value) {
    return '尚無訊息'
  }

  return new Date(value).toLocaleString('zh-TW', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export const ConversationsPage = () => {
  const [items, setItems] = useState<ConversationItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let disposed = false

    void messagingApi
      .listConversations()
      .then((data) => {
        if (!disposed) {
          setItems(data)
        }
      })
      .catch((err: unknown) => {
        if (disposed) {
          return
        }

        const message = err instanceof ApiClientError ? err.message : '讀取對話失敗'
        setError(message)
      })
      .finally(() => {
        if (!disposed) {
          setLoading(false)
        }
      })

    return () => {
      disposed = true
    }
  }, [])

  return (
    <main className="mx-auto w-full max-w-4xl px-4 py-6 md:py-8">
      <section className="mb-6 space-y-2">
        <h1 className="text-3xl font-semibold text-text-main sm:text-4xl">訊息中心</h1>
        <p className="text-text-subtle">管理與買家/賣家的所有對話。</p>
      </section>

      {error ? <p className="mb-4 text-sm text-danger">{error}</p> : null}

      {loading ? <Card className="h-40 animate-pulse bg-surface-2" /> : null}

      {!loading && !items.length ? (
        <EmptyState title="目前沒有任何對話" description="當你開始與其他使用者聯繫後，訊息會出現在這裡。" />
      ) : null}

      {!loading && items.length ? (
        <section className="space-y-3">
          {items.map((item) => (
            <Link to={`/messages/${item.conversationId}`} key={item.conversationId} className="block">
              <Card className="transition hover:border-brand">
                <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                  <div>
                    <p className="text-sm text-text-subtle">{item.listingTitle}</p>
                    <h2 className="text-lg font-semibold text-text-main">{item.otherDisplayName}</h2>
                    <p className="mt-1 line-clamp-1 text-sm text-text-muted">
                      {item.lastMessagePreview ?? '尚未有訊息內容'}
                    </p>
                  </div>
                  <div className="text-left sm:text-right">
                    <p className="text-xs text-text-muted">{formatDateTime(item.lastMessageAt)}</p>
                    {item.unreadCount > 0 ? (
                      <span className="mt-2 inline-block rounded-full bg-brand px-2 py-1 text-xs text-brand-foreground">
                        未讀 {item.unreadCount}
                      </span>
                    ) : null}
                  </div>
                </div>
              </Card>
            </Link>
          ))}
        </section>
      ) : null}
    </main>
  )
}
