import { useEffect, useMemo, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { CircleCheck } from 'lucide-react'
import { useAuth } from '@/features/auth/components/AuthProvider'
import { listingApi, type ListingDetail } from '@/features/listings/api/listingApi'
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

const formatPrice = (item: ListingDetail) => {
  if (item.isFree) {
    return '免費'
  }
  return `NT$ ${item.price.toLocaleString()}`
}

export const ChatPage = () => {
  const { conversationId = '' } = useParams()
  const { tokens } = useAuth()
  const [messages, setMessages] = useState<MessageItem[]>([])
  const [listingDetail, setListingDetail] = useState<ListingDetail | null>(null)
  const [listingTitle, setListingTitle] = useState<string | null>(null)
  const [listingLoading, setListingLoading] = useState(true)
  const [purchaseRequest, setPurchaseRequest] = useState<ConversationPurchaseRequest | null>(null)
  const [purchaseRequestFetchedAtMs, setPurchaseRequestFetchedAtMs] = useState(() => Date.now())
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
    setListingLoading(true)
    setListingDetail(null)
    setListingTitle(null)

    void messagingApi
      .listConversations()
      .then(async (conversations) => {
        if (disposed) {
          return
        }
        const current = conversations.find((item) => item.conversationId === conversationId)
        if (!current) {
          setListingLoading(false)
          return
        }

        setListingTitle(current.listingTitle)

        try {
          const detail = await listingApi.getById(current.listingId)
          if (!disposed) {
            setListingDetail(detail)
          }
        } catch {
          // Ignore listing detail loading failure; fall back to title-only block.
        } finally {
          if (!disposed) {
            setListingLoading(false)
          }
        }
      })
      .catch(() => {
        if (!disposed) {
          setListingLoading(false)
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
          setPurchaseRequestFetchedAtMs(Date.now())
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
      setPurchaseRequestFetchedAtMs(Date.now())
    } catch (err) {
      const message = err instanceof ApiClientError ? err.message : '交易操作失敗'
      setPurchaseRequestError(message)
    } finally {
      setPurchaseRequestBusy(false)
    }
  }

  const isPending = purchaseRequest?.status === PurchaseRequestStatus.Pending
  const isAccepted = purchaseRequest?.status === PurchaseRequestStatus.Accepted
  const isSeller = purchaseRequest?.sellerId === tokens?.userId
  const isBuyer = purchaseRequest?.buyerId === tokens?.userId
  const elapsedSinceRequestFetchSeconds = Math.max(0, Math.floor((countdownNowMs - purchaseRequestFetchedAtMs) / 1000))
  const remainingSeconds = !purchaseRequest
    ? null
    : Math.max(0, purchaseRequest.remainingSeconds - elapsedSinceRequestFetchSeconds)

  return (
    <main className="mx-auto w-full max-w-6xl px-3 pb-28 pt-4 sm:px-4 md:pb-0 md:py-8">
      <section className="mb-8 space-y-3 text-center">
        <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="text-5xl font-semibold leading-tight text-text-main sm:text-6xl md:text-7xl">
          對話<span className="marker-wipe">視窗</span>
        </h1>
      </section>
      <div className="mb-4">
        <Link to="/messages" className="text-base text-text-subtle hover:text-text-main md:text-sm">
          ← 返回訊息列表
        </Link>
      </div>
      <div className="grid gap-4 lg:grid-cols-[300px_minmax(0,1fr)]">
        <aside className="hidden lg:block">
          <Card className="h-full space-y-3">
            <h2 className="text-base font-semibold text-text-main">商品資訊</h2>
            {listingLoading ? <p className="text-sm text-text-subtle">載入商品中...</p> : null}
            {!listingLoading && listingDetail ? (
              <div className="space-y-3">
                <div className="overflow-hidden rounded-xl border border-border bg-surface-2">
                  {listingDetail.mainImageUrl ? (
                    <img src={listingDetail.mainImageUrl} alt={listingDetail.title} className="aspect-square w-full object-cover" />
                  ) : (
                    <div className="flex aspect-square items-center justify-center text-sm text-text-muted">無圖片</div>
                  )}
                </div>
                <h3 className="line-clamp-2 text-base font-semibold text-text-main">{listingDetail.title}</h3>
                <p className="text-lg font-bold text-text-main">{formatPrice(listingDetail)}</p>
                <p className="text-sm text-text-subtle">
                  {listingDetail.categoryName}・{listingDetail.conditionName}
                </p>
                <Link
                  to={`/listings/${listingDetail.id}?from=chat&conversationId=${conversationId}`}
                  className="inline-flex min-h-[2.5rem] w-full items-center justify-center rounded-xl border border-border bg-surface px-3 text-sm font-semibold text-text-main transition hover:bg-surface-2"
                >
                  查看商品頁
                </Link>
              </div>
            ) : null}
            {!listingLoading && !listingDetail && listingTitle ? (
              <div className="space-y-2">
                <p className="text-sm text-text-subtle">此對話對應商品</p>
                <p className="text-base font-semibold text-text-main">{listingTitle}</p>
              </div>
            ) : null}
          </Card>
        </aside>

        <section className="space-y-4">
          <div className="rounded-2xl border border-brand/60 bg-brand/60 px-4 pb-4 pt-0 text-brand-foreground shadow-soft md:p-4">
            {purchaseRequestLoading ? <p className="pt-2 text-base text-white/85 md:pt-0 md:text-xs">讀取交易狀態中...</p> : null}

            {purchaseRequest ? (
              <div className="mt-3">
                <div className="flex items-center justify-between gap-2">
                  <p className="inline-flex items-center gap-1.5 text-3xl font-semibold text-white md:text-xl">
                    交易狀態：{getPurchaseRequestStatusText(purchaseRequest.status)}
                    {isAccepted ? <CircleCheck className="h-5 w-5 text-[#D5F2DE] md:h-4 md:w-4" aria-hidden="true" /> : null}
                  </p>
                  {isPending && remainingSeconds != null ? (
                    <span className="rounded-full bg-black/70 px-2.5 py-1 text-base font-semibold text-white tabular-nums md:px-2 md:py-0.5 md:text-xs">
                      {formatCountdown(remainingSeconds)}
                    </span>
                  ) : null}
                </div>
                {isPending ? (
                  <div className="mt-2 flex flex-wrap gap-2">
                    {isSeller ? (
                      <>
                        <Button
                          type="button"
                          onClick={() => void handlePurchaseRequestAction('accept')}
                          disabled={purchaseRequestBusy}
                          className="text-lg md:text-sm"
                        >
                          {purchaseRequestBusy ? '處理中...' : '同意交易'}
                        </Button>
                        <Button
                          type="button"
                          variant="secondary"
                          onClick={() => void handlePurchaseRequestAction('reject')}
                          disabled={purchaseRequestBusy}
                          className="text-lg md:text-sm"
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
                        className="text-lg md:text-sm"
                      >
                        {purchaseRequestBusy ? '處理中...' : '取消請求'}
                      </Button>
                    ) : null}
                  </div>
                ) : null}
                {isAccepted ? (
                  <div className="mt-3">
                    <Link
                      to={`/purchase-requests/${purchaseRequest.id}/review`}
                      className="inline-flex min-h-[2.6rem] w-full items-center justify-center rounded-xl border border-white/35 bg-white/12 px-3 py-1.5 text-base font-semibold text-white transition hover:bg-white/18 md:w-auto md:text-sm"
                    >
                      交易完成後前往評價
                    </Link>
                  </div>
                ) : null}
              </div>
            ) : (
              <p className="mt-3 text-lg text-white/85 md:text-sm">目前尚未建立交易請求，可先透過聊天與對方溝通。</p>
            )}
            {purchaseRequestError ? <p className="mt-2 text-base text-[#FFD3D3] md:text-xs">{purchaseRequestError}</p> : null}
          </div>

          <div className="rounded-2xl border border-border bg-surface-2 shadow-soft">
            <section className="flex h-[calc(100vh-26rem)] min-h-[23rem] flex-col gap-3 overflow-y-auto px-3 py-3 pb-24 sm:px-4 sm:py-4 md:h-[58vh] md:pb-4">
          {loading ? <p className="text-lg text-text-subtle md:text-sm">載入訊息中...</p> : null}
          {error ? <p className="text-lg text-danger md:text-sm">{error}</p> : null}
          {!loading &&
            messages.map((message) => {
              const isMine = message.senderId === tokens?.userId
              return (
                <div key={message.id} className={`flex ${isMine ? 'justify-end' : 'justify-start'}`}>
                  <div
                    className={`max-w-[85%] rounded-2xl px-3 py-2 text-xl sm:max-w-[70%] sm:px-4 md:text-sm ${
                      isMine
                        ? 'bg-brand text-brand-foreground'
                        : 'border border-border bg-surface text-text-main'
                    }`}
                  >
                    <p>{message.content}</p>
                    <p className={`mt-1 text-sm md:text-[11px] ${isMine ? 'text-brand-foreground/80' : 'text-text-muted'}`}>
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
          </div>

          <div className="fixed inset-x-0 bottom-0 z-30 border-t border-border bg-surface p-3 shadow-[0_-8px_20px_rgba(0,0,0,0.08)] sm:p-4 md:static md:rounded-2xl md:border md:shadow-soft">
            <form onSubmit={handleSend} className="flex items-center gap-2">
              <input
                className="flex-1 rounded-xl border border-border bg-white px-3 py-2 text-xl outline-none focus:border-brand md:text-sm"
                placeholder="輸入訊息..."
                value={draft}
                onChange={(event) => setDraft(event.target.value)}
                maxLength={1000}
              />
              <Button type="submit" disabled={sending} className="shrink-0 text-xl md:text-sm">
                {sending ? '送出中' : '送出'}
              </Button>
            </form>
          </div>
        </section>
      </div>
    </main>
  )
}
