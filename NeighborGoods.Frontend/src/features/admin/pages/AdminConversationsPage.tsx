import { useCallback, useEffect, useState } from 'react'
import { adminApi, type AdminConversationByListing, type AdminConversationMessage } from '@/features/admin/api/adminApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { AppModal } from '@/shared/ui/modal/AppModal'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

const toLocalDate = (value: string) => {
  const trimmed = value.trim()
  // Backend may return UTC-like timestamps without timezone suffix.
  // Treat those as UTC explicitly to avoid displaying UTC as local time.
  const normalized = /(?:Z|[+-]\d{2}:\d{2})$/i.test(trimmed) ? trimmed : `${trimmed}Z`
  return new Date(normalized)
}

const formatDateTime = (value: string) =>
  toLocalDate(value).toLocaleString('zh-TW', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })

export const AdminConversationsPage = () => {
  const [data, setData] = useState<AdminConversationByListing | null>(null)
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [messages, setMessages] = useState<AdminConversationMessage[]>([])
  const [keyword, setKeyword] = useState('')
  const [draftMessage, setDraftMessage] = useState('')
  const [sending, setSending] = useState(false)
  const [messagesLoading, setMessagesLoading] = useState(false)
  const [messagesError, setMessagesError] = useState<string | null>(null)

  const loadConversations = useCallback(async (targetPage: number) => {
    setLoading(true)
    setError(null)
    try {
      const result = await adminApi.listConversationsByListing({ page: targetPage, pageSize: 10 })
      setData(result)
      setPage(result.page)
      setTotalPages(Math.max(result.totalPages, 1))
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '讀取聊天室列表失敗')
    } finally {
      setLoading(false)
    }
  }, [])

  const loadMessages = useCallback(async (conversationId: string, q?: string) => {
    setMessagesLoading(true)
    setMessagesError(null)
    try {
      const result = await adminApi.getConversationMessages(conversationId, { page: 1, pageSize: 200, q: q?.trim() || undefined })
      setMessages(result.items)
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

  const handleView = async (conversationId: string) => {
    setSelectedId(conversationId)
    setDraftMessage('')
    await loadMessages(conversationId)
  }

  const handleSendMessage = async () => {
    if (!selectedId || sending) {
      return
    }

    const trimmed = draftMessage.trim()
    if (!trimmed) {
      return
    }

    setSending(true)
    setMessagesError(null)
    try {
      const sent = await adminApi.postConversationMessage(selectedId, trimmed)
      setMessages((current) => [...current, sent])
      setDraftMessage('')
    } catch (err) {
      setMessagesError(err instanceof ApiClientError ? err.message : '送出訊息失敗')
    } finally {
      setSending(false)
    }
  }

  const selectedConversation = data?.items.flatMap((g) => g.conversations).find((item) => item.conversationId === selectedId) ?? null
  const selectedListing = data?.items.find((group) => group.conversations.some((conversation) => conversation.conversationId === selectedId)) ?? null

  if (loading && !data) {
    return <PageSkeleton className="h-96" />
  }

  if (error && !data) {
    return <ErrorState description={error} onRetry={() => void loadConversations(page)} />
  }

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold text-text-main">聊天室檢查</h1>

      {error ? <p className="text-sm text-rose-600">{error}</p> : null}

      <div className="space-y-3">
        {data?.items.length ? data.items.map((group) => (
          <div key={group.listingId} className="rounded-xl border border-border bg-surface p-3">
            <div className="mb-2 flex gap-3">
              {group.listingImageUrl ? (
                <img src={group.listingImageUrl} alt={group.listingTitle} className="h-16 w-16 rounded object-cover" />
              ) : (
                <div className="flex h-16 w-16 items-center justify-center rounded bg-surface-2 text-xs text-text-muted">無圖</div>
              )}
              <div className="min-w-0">
                <p className="truncate font-semibold text-text-main">
                  {group.listingTitle}（賣家：{group.sellerDisplayName}）
                </p>
                <p className="text-sm text-text-subtle">
                  {group.conversationCount} 個對話・最後更新 {formatDateTime(group.lastUpdatedAt)}
                </p>
              </div>
            </div>
            <div className="space-y-1">
              {group.conversations.map((item) => (
                <div key={item.conversationId} className="flex items-center justify-between rounded border border-border px-3 py-2 text-sm">
                  <p>
                    {item.participant1DisplayName} ↔ {item.participant2DisplayName}（{item.messageCount} 則）
                  </p>
                  <button type="button" className="underline" onClick={() => void handleView(item.conversationId)}>
                    查看
                  </button>
                </div>
              ))}
            </div>
          </div>
        )) : (
          <p className="text-sm text-text-muted">目前沒有聊天室</p>
        )}
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

      <AppModal open={Boolean(selectedId)} onClose={() => setSelectedId(null)} maxWidthClassName="max-w-3xl">
        <div className="space-y-3">
          <p className="font-semibold">
            對話紀錄
            {selectedConversation && selectedListing
              ? `：${selectedConversation.participant1DisplayName} ↔ ${selectedConversation.participant2DisplayName}（${selectedListing.listingTitle}）`
              : ''}
          </p>
          <div className="flex gap-2">
            <input
              className="w-full rounded-xl border border-border bg-surface px-3 py-2 text-text-main outline-none transition placeholder:text-text-muted focus:border-brand"
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
              placeholder="搜尋關鍵字"
            />
            <Button type="button" variant="secondary" onClick={() => selectedId && void loadMessages(selectedId, keyword)}>
              查詢
            </Button>
          </div>
          {messagesLoading ? <p className="text-sm text-text-subtle">載入中...</p> : null}
          {messagesError ? <p className="text-sm text-rose-600">{messagesError}</p> : null}
          {!messagesLoading && !messagesError ? (
            messages.length === 0 ? (
              <p className="text-sm text-text-muted">查無訊息</p>
            ) : (
              <div className="max-h-[60vh] space-y-1 overflow-y-auto font-mono text-sm">
                {messages.map((message) => (
                  <div key={message.id}>
                    [{formatDateTime(message.createdAt)}] {message.senderDisplayName}：{message.content}
                  </div>
                ))}
              </div>
            )
          ) : null}

          <div className="border-t border-border pt-3">
            <p className="mb-2 text-sm text-text-subtle">以管理員身分回覆</p>
            <div className="flex gap-2">
              <input
                className="w-full rounded-xl border border-border bg-surface px-3 py-2 text-text-main outline-none transition placeholder:text-text-muted focus:border-brand"
                value={draftMessage}
                onChange={(e) => setDraftMessage(e.target.value)}
                placeholder="輸入要回覆的內容"
              />
              <Button
                type="button"
                onClick={() => void handleSendMessage()}
                disabled={sending || !selectedId || draftMessage.trim().length === 0}
              >
                送出
              </Button>
            </div>
          </div>
        </div>
      </AppModal>
    </div>
  )
}
