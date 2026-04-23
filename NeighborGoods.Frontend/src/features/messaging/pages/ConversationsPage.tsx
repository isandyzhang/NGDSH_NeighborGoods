import { useEffect, useState } from 'react'
import { motion } from 'framer-motion'
import { Link } from 'react-router-dom'
import { messagingApi, type ConversationItem } from '@/features/messaging/api/messagingApi'
import { ApiClientError } from '@/shared/types/api'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

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
      <section className="mb-8 space-y-3 text-center">
        <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="text-5xl font-semibold leading-tight text-text-main sm:text-6xl md:text-7xl">
          訊息<span className="marker-wipe">中心</span>
        </h1>
        <p className="mx-auto max-w-2xl text-lg text-text-subtle">管理與買家/賣家的所有對話。</p>
      </section>

      {error ? <ErrorState description={error} /> : null}

      {loading ? <PageSkeleton className="h-40" /> : null}

      {!loading && !items.length ? (
        <EmptyState title="目前沒有任何對話" description="當你開始與其他使用者聯繫後，訊息會出現在這裡。" />
      ) : null}

      {!loading && items.length ? (
        <motion.section
          className="space-y-3"
          initial="hidden"
          animate="visible"
          variants={{
            visible: {
              transition: {
                staggerChildren: 0.08,
              },
            },
          }}
        >
          {items.map((item) => (
            <motion.div
              key={item.conversationId}
              variants={{
                hidden: { opacity: 0, y: 14 },
                visible: { opacity: 1, y: 0 },
              }}
              transition={{ duration: 0.3, ease: [0.22, 1, 0.36, 1] }}
            >
              <Link to={`/messages/${item.conversationId}`} className="block">
                <Card className="transition hover:border-brand">
                  <div className="space-y-2">
                    <div className="flex items-baseline gap-2">
                      <h2 className="text-2xl font-bold leading-none text-text-main">{item.otherDisplayName}</h2>
                      <p className="line-clamp-1 text-sm text-text-subtle">{item.listingTitle}</p>
                    </div>
                    <div className="flex items-center justify-between gap-3">
                      <p className="line-clamp-1 text-sm text-text-muted">{item.lastMessagePreview ?? '尚未有訊息內容'}</p>
                      <p className="shrink-0 text-xs text-text-muted">{formatDateTime(item.lastMessageAt)}</p>
                    </div>
                    {item.unreadCount > 0 ? (
                      <span className="inline-block rounded-full bg-brand px-2 py-1 text-xs text-brand-foreground">
                        未讀 {item.unreadCount}
                      </span>
                    ) : null}
                  </div>
                </Card>
              </Link>
            </motion.div>
          ))}
        </motion.section>
      ) : null}
    </main>
  )
}
