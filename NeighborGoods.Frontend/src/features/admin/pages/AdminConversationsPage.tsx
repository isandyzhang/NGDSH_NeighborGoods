import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  adminApi,
  type AdminConversationListItem,
  type AdminConversationMessage,
} from '@/features/admin/api/adminApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

const formatDateTime = (value: string) =>
  new Date(value).toLocaleString('zh-TW', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })

const truncate = (value: string | null, max = 40) => {
  if (!value) {
    return '-'
  }
  return value.length > max ? `${value.slice(0, max)}…` : value
}

export const AdminConversationsPage = () => {
  const [items, setItems] = useState<AdminConversationListItem[]>([])
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [messages, setMessages] = useState<AdminConversationMessage[]>([])
  const [messagesLoading, setMessagesLoading] = useState(false)
  const [messagesError, setMessagesError] = useState<string | null>(null)
  const [messagePage, setMessagePage] = useState(1)
  const [messageTotalPages, setMessageTotalPages] = useState(1)
  const messagesPanelRef = useRef<HTMLDivElement>(null)

  const loadConversations = useCallback(async (targetPage: number) => {
    setLoading(true)
    setError(null)
    try {
      const result = await adminApi.listConversations({ page: targetPage, pageSize: 50 })
      setItems(result.items)
      setPage(result.page)
      setTotalPages(Math.max(result.totalPages, 1))
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '讀取聊天室列表失敗')
    } finally {
      setLoading(false)
    }
  }, [])

  const loadMessages = useCallback(async (conversationId: string, targetPage: number) => {
    setMessagesLoading(true)
    setMessagesError(null)
    try {
      const result = await adminApi.getConversationMessages(conversationId, { page: targetPage, pageSize: 100 })
      setMessages(result.items)
      setMessagePage(result.page)
      setMessageTotalPages(Math.max(result.totalPages, 1))
    } catch (err) {
      setMessagesError(err instanceof ApiClientError ? err.message : '讀取對話紀錄失敗')
      setMessages([])
    } finally {
      setMessagesLoading(false)
    }
  }, [])

  useEffect(() => {
    void loadConversations(page)
  }, [loadConversations, page])

  const handleView = async (conversation: AdminConversationListItem) => {
    setSelectedId(conversation.conversationId)
    setMessagePage(1)
    await loadMessages(conversation.conversationId, 1)
    requestAnimationFrame(() => {
      messagesPanelRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    })
  }

  if (loading && items.length === 0) {
    return <PageSkeleton className="h-96" />
  }

  if (error && items.length === 0) {
    return <ErrorState description={error} onRetry={() => void loadConversations(page)} />
  }

  const selectedConversation = items.find((item) => item.conversationId === selectedId) ?? null

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-2xl font-semibold text-text-main">聊天室列表</h1>
        <Link to="/admin" className="text-sm text-text-subtle underline">
          返回後台首頁
        </Link>
      </div>

      {error ? <p className="text-sm text-rose-600">{error}</p> : null}

      <div className="overflow-x-auto border border-border">
        <table className="min-w-full border-collapse text-left text-sm">
          <thead className="bg-surface-2">
            <tr>
              <th className="border border-border px-2 py-2">更新時間</th>
              <th className="border border-border px-2 py-2">商品</th>
              <th className="border border-border px-2 py-2">參與者 A</th>
              <th className="border border-border px-2 py-2">參與者 B</th>
              <th className="border border-border px-2 py-2">訊息數</th>
              <th className="border border-border px-2 py-2">最後訊息</th>
              <th className="border border-border px-2 py-2">操作</th>
            </tr>
          </thead>
          <tbody>
            {items.length === 0 ? (
              <tr>
                <td className="border border-border px-2 py-4 text-text-muted" colSpan={7}>
                  目前沒有聊天室
                </td>
              </tr>
            ) : (
              items.map((item) => (
                <tr key={item.conversationId} className={selectedId === item.conversationId ? 'bg-surface-2' : ''}>
                  <td className="border border-border px-2 py-2 whitespace-nowrap">{formatDateTime(item.updatedAt)}</td>
                  <td className="border border-border px-2 py-2">{item.listingTitle}</td>
                  <td className="border border-border px-2 py-2">{item.participant1DisplayName}</td>
                  <td className="border border-border px-2 py-2">{item.participant2DisplayName}</td>
                  <td className="border border-border px-2 py-2">{item.messageCount}</td>
                  <td className="border border-border px-2 py-2">{truncate(item.lastMessagePreview)}</td>
                  <td className="border border-border px-2 py-2">
                    <button
                      type="button"
                      className="underline"
                      onClick={() => void handleView(item)}
                    >
                      {selectedId === item.conversationId ? '查看中' : '查看'}
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {totalPages > 1 ? (
        <div className="flex items-center gap-3">
          <Button type="button" variant="secondary" disabled={page <= 1 || loading} onClick={() => setPage((p) => p - 1)}>
            上一頁
          </Button>
          <span className="text-sm text-text-subtle">
            第 {page} / {totalPages} 頁
          </span>
          <Button
            type="button"
            variant="secondary"
            disabled={page >= totalPages || loading}
            onClick={() => setPage((p) => p + 1)}
          >
            下一頁
          </Button>
        </div>
      ) : null}

      {selectedId ? (
        <div ref={messagesPanelRef} className="scroll-mt-4 border border-border bg-surface p-3">
          <p className="mb-2 font-semibold">
            對話紀錄
            {selectedConversation
              ? `：${selectedConversation.participant1DisplayName} ↔ ${selectedConversation.participant2DisplayName}（${selectedConversation.listingTitle}）`
              : ''}
          </p>

          {messagesLoading ? <p className="text-sm text-text-subtle">載入中...</p> : null}
          {messagesError ? <p className="text-sm text-rose-600">{messagesError}</p> : null}

          {!messagesLoading && !messagesError ? (
            messages.length === 0 ? (
              <p className="text-sm text-text-muted">此對話尚無訊息</p>
            ) : (
              <div className="space-y-1 font-mono text-sm">
                {messages.map((message) => (
                  <div key={message.id}>
                    [{formatDateTime(message.createdAt)}] {message.senderDisplayName}：{message.content}
                  </div>
                ))}
              </div>
            )
          ) : null}

          {messageTotalPages > 1 ? (
            <div className="mt-3 flex items-center gap-3">
              <Button
                type="button"
                variant="secondary"
                disabled={messagePage <= 1 || messagesLoading}
                onClick={() => {
                  const nextPage = messagePage - 1
                  setMessagePage(nextPage)
                  void loadMessages(selectedId, nextPage)
                }}
              >
                較舊訊息
              </Button>
              <span className="text-sm text-text-subtle">
                第 {messagePage} / {messageTotalPages} 頁
              </span>
              <Button
                type="button"
                variant="secondary"
                disabled={messagePage >= messageTotalPages || messagesLoading}
                onClick={() => {
                  const nextPage = messagePage + 1
                  setMessagePage(nextPage)
                  void loadMessages(selectedId, nextPage)
                }}
              >
                較新訊息
              </Button>
            </div>
          ) : null}
        </div>
      ) : null}
    </div>
  )
}
