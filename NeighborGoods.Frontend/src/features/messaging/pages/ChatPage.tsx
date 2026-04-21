import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useAuth } from '@/features/auth/components/AuthProvider'
import { MessageHubClient } from '@/features/messaging/services/messageHub'
import { messagingApi, type MessageItem } from '@/features/messaging/api/messagingApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'

const mergeMessages = (base: MessageItem[], incoming: MessageItem[]) => {
  const map = new Map<string, MessageItem>()
  for (const item of base) {
    map.set(item.id, item)
  }
  for (const item of incoming) {
    map.set(item.id, item)
  }
  return [...map.values()].sort(
    (left, right) => new Date(left.createdAt).getTime() - new Date(right.createdAt).getTime(),
  )
}

export const ChatPage = () => {
  const { conversationId = '' } = useParams()
  const { tokens } = useAuth()
  const [messages, setMessages] = useState<MessageItem[]>([])
  const [draft, setDraft] = useState('')
  const [loading, setLoading] = useState(true)
  const [sending, setSending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const bottomRef = useRef<HTMLDivElement | null>(null)

  const hubClient = useMemo(() => new MessageHubClient(), [])

  useEffect(() => {
    let disposed = false
    setLoading(true)
    setError(null)

    void messagingApi
      .getMessages(conversationId)
      .then((data) => {
        if (!disposed) {
          setMessages(data.items)
          void messagingApi.markRead(conversationId)
        }
      })
      .catch((err: unknown) => {
        if (disposed) {
          return
        }
        const message = err instanceof ApiClientError ? err.message : '讀取訊息失敗'
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
  }, [conversationId])

  useEffect(() => {
    if (!tokens?.accessToken || !conversationId) {
      return
    }

    void hubClient.connect(tokens.accessToken, (message) => {
      setMessages((current) => mergeMessages(current, [message]))
      void messagingApi.markRead(conversationId)
    })

    return () => {
      void hubClient.disconnect()
    }
  }, [conversationId, hubClient, tokens?.accessToken])

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages.length])

  const handleSend = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const content = draft.trim()
    if (!content) {
      return
    }

    setSending(true)
    setError(null)

    try {
      const message = await messagingApi.sendMessage(conversationId, content)
      setMessages((current) => mergeMessages(current, [message]))
      setDraft('')
    } catch (err) {
      const message = err instanceof ApiClientError ? err.message : '送出訊息失敗'
      setError(message)
    } finally {
      setSending(false)
    }
  }

  return (
    <main className="mx-auto w-full max-w-4xl px-3 py-4 sm:px-4 md:py-8">
      <div className="mb-4">
        <Link to="/messages" className="text-sm text-text-subtle hover:text-text-main">
          ← 返回訊息列表
        </Link>
      </div>
      <Card className="h-[calc(100vh-8.5rem)] min-h-[34rem] p-0 md:h-[70vh]">
        <header className="border-b border-border px-4 py-3">
          <h1 className="text-lg font-semibold text-text-main">對話視窗</h1>
          <p className="text-xs text-text-muted">Conversation ID: {conversationId}</p>
        </header>
        <section className="flex h-[calc(100%-8.5rem)] flex-col gap-3 overflow-y-auto bg-surface-2 px-3 py-3 sm:px-4 sm:py-4">
          {loading ? <p className="text-sm text-text-subtle">載入訊息中...</p> : null}
          {error ? <p className="text-sm text-danger">{error}</p> : null}
          {!loading &&
            messages.map((message) => {
              const isMine = message.senderId === tokens?.userId
              return (
                <div key={message.id} className={`flex ${isMine ? 'justify-end' : 'justify-start'}`}>
                  <div
                    className={`max-w-[85%] rounded-2xl px-3 py-2 text-sm sm:max-w-[70%] sm:px-4 ${
                      isMine
                        ? 'bg-brand text-brand-foreground'
                        : 'border border-border bg-surface text-text-main'
                    }`}
                  >
                    <p>{message.content}</p>
                    <p className={`mt-1 text-[11px] ${isMine ? 'text-brand-foreground/80' : 'text-text-muted'}`}>
                      {message.senderDisplayName}・
                      {new Date(message.createdAt).toLocaleTimeString('zh-TW', {
                        hour: '2-digit',
                        minute: '2-digit',
                      })}
                    </p>
                  </div>
                </div>
              )
            })}
          <div ref={bottomRef} />
        </section>
        <form
          onSubmit={handleSend}
          className="flex flex-col gap-2 border-t border-border bg-surface px-3 py-3 sm:flex-row sm:px-4"
        >
          <input
            className="flex-1 rounded-xl border border-border bg-white px-3 py-2 text-sm outline-none focus:border-brand"
            placeholder="輸入訊息..."
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            maxLength={1000}
          />
          <Button type="submit" disabled={sending} className="sm:w-auto">
            {sending ? '送出中' : '送出'}
          </Button>
        </form>
      </Card>
    </main>
  )
}
