import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useAuth } from '@/features/auth/components/AuthProvider'
import { MessageHubClient } from '@/features/messaging/services/messageHub'
import { messagingApi, type ConversationPurchaseRequest, type MessageItem } from '@/features/messaging/api/messagingApi'
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

const PurchaseRequestStatus = {
  Pending: 0,
  Accepted: 1,
  Rejected: 2,
  Expired: 3,
  Cancelled: 4,
} as const

const formatCountdown = (seconds: number) => {
  const normalized = Math.max(0, Math.floor(seconds))
  const hours = Math.floor(normalized / 3600)
  const minutes = Math.floor((normalized % 3600) / 60)
  const remainingSeconds = normalized % 60
  return [hours, minutes, remainingSeconds].map((value) => value.toString().padStart(2, '0')).join(':')
}

const getPurchaseRequestStatusText = (status: number) => {
  switch (status) {
    case PurchaseRequestStatus.Pending:
      return '待回覆'
    case PurchaseRequestStatus.Accepted:
      return '已同意'
    case PurchaseRequestStatus.Rejected:
      return '已拒絕'
    case PurchaseRequestStatus.Expired:
      return '已逾時'
    case PurchaseRequestStatus.Cancelled:
      return '已取消'
    default:
      return '未知狀態'
  }
}

export const ChatPage = () => {
  const { conversationId = '' } = useParams()
  const { tokens } = useAuth()
  const [messages, setMessages] = useState<MessageItem[]>([])
  const [purchaseRequest, setPurchaseRequest] = useState<ConversationPurchaseRequest | null>(null)
  const [purchaseRequestLoading, setPurchaseRequestLoading] = useState(true)
  const [purchaseRequestBusy, setPurchaseRequestBusy] = useState(false)
  const [purchaseRequestError, setPurchaseRequestError] = useState<string | null>(null)
  const [countdownNowMs, setCountdownNowMs] = useState(() => Date.now())
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
    let disposed = false
    setPurchaseRequestLoading(true)
    setPurchaseRequestError(null)
    void messagingApi
      .getCurrentPurchaseRequest(conversationId)
      .then((data) => {
        if (!disposed) {
          setPurchaseRequest(data)
        }
      })
      .catch((err) => {
        if (disposed) {
          return
        }
        if (err instanceof ApiClientError && err.code === 'PURCHASE_REQUEST_NOT_FOUND') {
          setPurchaseRequest(null)
          return
        }

        const message = err instanceof ApiClientError ? err.message : '讀取交易請求失敗'
        setPurchaseRequestError(message)
      })
      .finally(() => {
        if (!disposed) {
          setPurchaseRequestLoading(false)
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
    if (!purchaseRequest || purchaseRequest.status !== PurchaseRequestStatus.Pending) {
      return
    }

    const timer = window.setInterval(() => {
      setCountdownNowMs(Date.now())
    }, 1000)

    return () => {
      window.clearInterval(timer)
    }
  }, [purchaseRequest])

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

  const handlePurchaseRequestAction = async (action: 'accept' | 'reject' | 'cancel') => {
    if (!conversationId || purchaseRequestBusy) {
      return
    }

    setPurchaseRequestBusy(true)
    setPurchaseRequestError(null)

    try {
      const updated =
        action === 'accept'
          ? await messagingApi.acceptPurchaseRequest(conversationId)
          : action === 'reject'
            ? await messagingApi.rejectPurchaseRequest(
                conversationId,
                window.prompt('可選：輸入拒絕原因（可留空）') ?? undefined,
              )
            : await messagingApi.cancelPurchaseRequest(conversationId)
      setPurchaseRequest(updated)
    } catch (err) {
      const message = err instanceof ApiClientError ? err.message : '交易操作失敗'
      setPurchaseRequestError(message)
    } finally {
      setPurchaseRequestBusy(false)
    }
  }

  const isPending = purchaseRequest?.status === PurchaseRequestStatus.Pending
  const isSeller = purchaseRequest?.sellerId === tokens?.userId
  const isBuyer = purchaseRequest?.buyerId === tokens?.userId
  const remainingSeconds = !purchaseRequest
    ? null
    : Math.max(0, Math.floor((new Date(purchaseRequest.expireAt).getTime() - countdownNowMs) / 1000))

  return (
    <main className="mx-auto w-full max-w-4xl px-3 py-4 sm:px-4 md:py-8">
      <section className="mb-8 space-y-3 text-center">
        <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="text-5xl font-semibold leading-tight text-text-main sm:text-6xl md:text-7xl">
          對話<span className="marker-wipe">視窗</span>
        </h1>
      </section>
      <div className="mb-4">
        <Link to="/messages" className="text-sm text-text-subtle hover:text-text-main">
          ← 返回訊息列表
        </Link>
      </div>
      {purchaseRequest ? (
        <div className="mb-4 flex justify-end">
          <Link
            to={`/purchase-requests/${purchaseRequest.id}/review`}
            className="rounded-xl border border-border bg-surface px-4 py-2 text-sm font-semibold text-text-main transition hover:bg-surface-2"
          >
            交易完成後前往評價
          </Link>
        </div>
      ) : null}
      <Card className="h-[calc(100vh-8.5rem)] min-h-[34rem] p-0 md:h-[70vh]">
        <header className="border-b border-border px-4 py-3">
          <h2 className="text-lg font-semibold text-text-main">對話詳情</h2>
          <p className="text-xs text-text-muted">Conversation ID: {conversationId}</p>
          {purchaseRequestLoading ? (
            <p className="mt-2 text-xs text-text-muted">讀取交易狀態中...</p>
          ) : null}
          {purchaseRequest ? (
            <div className="mt-2 rounded-xl border border-border bg-surface-2 p-3">
              <div className="flex items-center justify-between gap-2">
                <p className="text-sm font-semibold text-text-main">交易狀態：{getPurchaseRequestStatusText(purchaseRequest.status)}</p>
                {isPending && remainingSeconds != null ? (
                  <span className="rounded-full bg-black/70 px-2 py-0.5 text-xs font-semibold text-white tabular-nums">
                    {formatCountdown(remainingSeconds)}
                  </span>
                ) : null}
              </div>
              {isPending ? (
                <div className="mt-2 flex flex-wrap gap-2">
                  {isSeller ? (
                    <>
                      <Button type="button" onClick={() => void handlePurchaseRequestAction('accept')} disabled={purchaseRequestBusy}>
                        {purchaseRequestBusy ? '處理中...' : '同意交易'}
                      </Button>
                      <Button
                        type="button"
                        variant="secondary"
                        onClick={() => void handlePurchaseRequestAction('reject')}
                        disabled={purchaseRequestBusy}
                      >
                        {purchaseRequestBusy ? '處理中...' : '拒絕交易'}
                      </Button>
                    </>
                  ) : null}
                  {isBuyer ? (
                    <Button
                      type="button"
                      variant="secondary"
                      onClick={() => void handlePurchaseRequestAction('cancel')}
                      disabled={purchaseRequestBusy}
                    >
                      {purchaseRequestBusy ? '處理中...' : '取消請求'}
                    </Button>
                  ) : null}
                </div>
              ) : null}
            </div>
          ) : null}
          {purchaseRequestError ? <p className="mt-2 text-xs text-danger">{purchaseRequestError}</p> : null}
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
