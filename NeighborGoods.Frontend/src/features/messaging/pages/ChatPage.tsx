import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { CircleCheck } from 'lucide-react'
import { useAuth } from '@/features/auth/components/AuthProvider'
import { listingApi, type ListingDetail } from '@/features/listings/api/listingApi'
import { PurchaseConfirmModal } from '@/features/listings/components/PurchaseConfirmModal'
import { TradeActionConfirmModal } from '@/features/messaging/components/TradeActionConfirmModal'
import { messagingApi, type ConversationPurchaseRequest, type MessageItem } from '@/features/messaging/api/messagingApi'
import { useSharedMessageHub } from '@/features/messaging/context/SharedMessageHubProvider'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'

const parseApiDateToMs = (value: string) => {
  const hasTimezone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(value)
  const normalized = hasTimezone ? value : `${value}Z`
  const parsed = Date.parse(normalized)
  return Number.isNaN(parsed) ? 0 : parsed
}

const formatTaipeiTime = (value: string) =>
  new Date(parseApiDateToMs(value)).toLocaleTimeString('zh-TW', {
    timeZone: 'Asia/Taipei',
    hour: '2-digit',
    minute: '2-digit',
  })

const mergeMessages = (base: MessageItem[], incoming: MessageItem[], knownMessageIds: Set<string>) => {
  if (incoming.length === 0) {
    return base
  }

  let next = base
  for (const message of incoming) {
    if (knownMessageIds.has(message.id)) {
      continue
    }
    knownMessageIds.add(message.id)

    const messageTime = parseApiDateToMs(message.createdAt)
    const last = next.at(-1)
    if (!last || parseApiDateToMs(last.createdAt) <= messageTime) {
      next = [...next, message]
      continue
    }

    const insertAt = next.findIndex((item) => parseApiDateToMs(item.createdAt) > messageTime)
    if (insertAt < 0) {
      next = [...next, message]
      continue
    }

    next = [...next.slice(0, insertAt), message, ...next.slice(insertAt)]
  }

  return next
}

const PurchaseRequestStatus = {
  Pending: 0,
  Accepted: 1,
  Rejected: 2,
  Expired: 3,
  Cancelled: 4,
  SellerMarkedCompleted: 5,
  Completed: 6,
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
    case PurchaseRequestStatus.SellerMarkedCompleted:
      return '待買家確認'
    case PurchaseRequestStatus.Completed:
      return '已完成'
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
  const [purchaseRequestCreating, setPurchaseRequestCreating] = useState(false)
  const [purchaseConfirmOpen, setPurchaseConfirmOpen] = useState(false)
  const [confirmModalAction, setConfirmModalAction] = useState<'completeBySeller' | 'confirmReceivedByBuyer' | null>(null)
  const [purchaseRequestError, setPurchaseRequestError] = useState<string | null>(null)
  const [countdownNowMs, setCountdownNowMs] = useState(() => Date.now())
  const [draft, setDraft] = useState('')
  const [loading, setLoading] = useState(true)
  const [sending, setSending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const markReadTimerRef = useRef<number | null>(null)
  const knownMessageIdsRef = useRef<Set<string>>(new Set())
  const bottomRef = useRef<HTMLDivElement | null>(null)
  const { connection, hubReady, joinConversation, leaveConversation } = useSharedMessageHub()
  const scheduleMarkRead = useCallback(() => {
    if (!conversationId || markReadTimerRef.current != null) {
      return
    }

    markReadTimerRef.current = window.setTimeout(() => {
      markReadTimerRef.current = null
      void messagingApi.markRead(conversationId).catch(() => {
        // ignore background mark-read failures
      })
    }, 1000)
  }, [conversationId])
  const refreshPurchaseRequest = useCallback(
    async (showLoading = false) => {
      if (!conversationId) {
        return
      }

      if (showLoading) {
        setPurchaseRequestLoading(true)
      }

      try {
        const data = await messagingApi.getCurrentPurchaseRequest(conversationId)
        setPurchaseRequest(data)
        setPurchaseRequestFetchedAtMs(Date.now())
        setPurchaseRequestError(null)
      } catch (err) {
        if (err instanceof ApiClientError && err.code === 'PURCHASE_REQUEST_NOT_FOUND') {
          setPurchaseRequest(null)
          setPurchaseRequestError(null)
          return
        }

        const message = err instanceof ApiClientError ? err.message : '讀取交易請求失敗'
        setPurchaseRequestError(message)
      } finally {
        if (showLoading) {
          setPurchaseRequestLoading(false)
        }
      }
    },
    [conversationId],
  )

  useEffect(() => {
    let disposed = false
    setLoading(true)
    setError(null)
    knownMessageIdsRef.current = new Set()

    void messagingApi
      .getMessages(conversationId)
      .then((data) => {
        if (!disposed) {
          knownMessageIdsRef.current = new Set(data.items.map((item) => item.id))
          setMessages(data.items)
          scheduleMarkRead()
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
  }, [conversationId, scheduleMarkRead])

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
    void refreshPurchaseRequest(true)
  }, [refreshPurchaseRequest])

  useEffect(() => {
    if (!conversationId || !hubReady || !connection) {
      return
    }

    const handler = (message: MessageItem) => {
      if (message.conversationId !== conversationId) {
        return
      }

      setMessages((current) => mergeMessages(current, [message], knownMessageIdsRef.current))
      scheduleMarkRead()

      if (message.content.startsWith('[系統發送]')) {
        void refreshPurchaseRequest()
      }
    }

    connection.on('ReceiveMessage', handler)

    void joinConversation(conversationId).catch((err: unknown) => {
      if (err instanceof Error && /stopped during negotiation|aborterror/i.test(err.message)) {
        return
      }
      console.warn('SignalR join failed', err)
    })

    return () => {
      connection.off('ReceiveMessage', handler)
      void leaveConversation(conversationId)
    }
  }, [conversationId, hubReady, connection, joinConversation, leaveConversation, refreshPurchaseRequest, scheduleMarkRead])

  useEffect(() => {
    return () => {
      if (markReadTimerRef.current != null) {
        window.clearTimeout(markReadTimerRef.current)
      }
    }
  }, [])

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
      setMessages((current) => mergeMessages(current, [message], knownMessageIdsRef.current))
      setDraft('')
    } catch (err) {
      const message = err instanceof ApiClientError ? err.message : '送出訊息失敗'
      setError(message)
    } finally {
      setSending(false)
    }
  }

  const handlePurchaseRequestAction = async (
    action: 'accept' | 'reject' | 'cancel' | 'completeBySeller' | 'confirmReceivedByBuyer',
  ) => {
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
            : action === 'cancel'
              ? await messagingApi.cancelPurchaseRequest(conversationId)
              : action === 'completeBySeller'
                ? await messagingApi.completeBySeller(conversationId)
                : await messagingApi.confirmReceivedByBuyer(conversationId)
      setPurchaseRequest(updated)
      setPurchaseRequestFetchedAtMs(Date.now())
    } catch (err) {
      const message = err instanceof ApiClientError ? err.message : '交易操作失敗'
      setPurchaseRequestError(message)
    } finally {
      setPurchaseRequestBusy(false)
    }
  }

  const openPurchaseConfirm = () => {
    if (!listingDetail) {
      return
    }
    setPurchaseConfirmOpen(true)
  }

  const handleCreatePurchaseRequest = async () => {
    if (!listingDetail || purchaseRequestCreating) {
      return
    }

    setPurchaseRequestCreating(true)
    setPurchaseRequestError(null)

    try {
      const created = await listingApi.createPurchaseRequest(listingDetail.id)
      setPurchaseConfirmOpen(false)
      setPurchaseRequest(created)
      setPurchaseRequestFetchedAtMs(Date.now())
    } catch (err) {
      const message = err instanceof ApiClientError ? err.message : '送出購買請求失敗'
      setPurchaseRequestError(message)
    } finally {
      setPurchaseRequestCreating(false)
    }
  }

  const isPending = purchaseRequest?.status === PurchaseRequestStatus.Pending
  const isAccepted = purchaseRequest?.status === PurchaseRequestStatus.Accepted
  const isSellerMarkedCompleted = purchaseRequest?.status === PurchaseRequestStatus.SellerMarkedCompleted
  const isCompleted = purchaseRequest?.status === PurchaseRequestStatus.Completed
  const isSeller = purchaseRequest?.sellerId === tokens?.userId
  const isBuyer = purchaseRequest?.buyerId === tokens?.userId
  const listingSellerId = listingDetail?.seller.id ?? null
  const isListingSeller = listingSellerId != null && listingSellerId === tokens?.userId
  const showQuickPurchaseButton =
    Boolean(listingDetail) &&
    !isListingSeller &&
    (!purchaseRequest ||
      purchaseRequest.status === PurchaseRequestStatus.Rejected ||
      purchaseRequest.status === PurchaseRequestStatus.Expired ||
      purchaseRequest.status === PurchaseRequestStatus.Cancelled)
  const elapsedSinceRequestFetchSeconds = Math.max(0, Math.floor((countdownNowMs - purchaseRequestFetchedAtMs) / 1000))
  const remainingSeconds = !purchaseRequest
    ? null
    : Math.max(0, purchaseRequest.remainingSeconds - elapsedSinceRequestFetchSeconds)

  const confirmModalCopy =
    confirmModalAction === 'completeBySeller'
      ? {
          title: '確認已與買家完成交易？',
          message: '送出後將把交易狀態更新為「待買家確認收貨」，並通知買家進行下一步。',
          finalConfirmLabel: '確定完成交易',
        }
      : confirmModalAction === 'confirmReceivedByBuyer'
        ? {
            title: '確認已與賣家完成交易',
            message: '送出後交易狀態會更新為「已完成」，你與賣家都可以前往評價。',
            finalConfirmLabel: '確定已收到商品',
          }
        : null

  const actionButtonClassName = 'min-h-[2.8rem] px-4 !text-[1.7rem] font-semibold md:!text-[1.125rem]'
  const purchaseButtonClassName = actionButtonClassName
  const primaryActionClassName = `${actionButtonClassName} bg-[#2F7D4E] text-white hover:bg-[#25633f]`
  const dangerActionClassName = `${actionButtonClassName} bg-[#B42318] text-white hover:bg-[#8f1c13]`
  const neutralActionClassName = `${actionButtonClassName} border border-white/35 bg-white/12 text-white hover:bg-white/18`

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
                <div className="flex items-start justify-between gap-3">
                  <div className="flex min-w-0 items-center gap-2">
                    <p className="inline-flex items-center gap-1.5 text-[1.7rem] font-semibold text-white md:text-[1.125rem]">
                      交易狀態：{getPurchaseRequestStatusText(purchaseRequest.status)}
                      {isAccepted ? <CircleCheck className="h-5 w-5 text-[#D5F2DE] md:h-4 md:w-4" aria-hidden="true" /> : null}
                    </p>
                  </div>
                  <div className="ml-auto flex shrink-0 flex-wrap justify-end gap-2">
                    {showQuickPurchaseButton ? (
                      <Button
                        type="button"
                        onClick={openPurchaseConfirm}
                        disabled={purchaseRequestCreating}
                        variant="secondary"
                        className={purchaseButtonClassName}
                      >
                        {purchaseRequestCreating ? <span className="font-extrabold">送出中...</span> : '購買'}
                      </Button>
                    ) : null}
                    {isPending && isBuyer ? (
                      <Button
                        type="button"
                        onClick={() => void handlePurchaseRequestAction('cancel')}
                        disabled={purchaseRequestBusy}
                        className={dangerActionClassName}
                      >
                        {purchaseRequestBusy ? '處理中...' : '取消請求'}
                      </Button>
                    ) : null}
                    {isAccepted && isSeller ? (
                      <Button
                        type="button"
                        onClick={() => setConfirmModalAction('completeBySeller')}
                        disabled={purchaseRequestBusy}
                        className={primaryActionClassName}
                      >
                        {purchaseRequestBusy ? '處理中...' : '完成交易'}
                      </Button>
                    ) : null}
                    {isSellerMarkedCompleted && isBuyer ? (
                      <Button
                        type="button"
                        onClick={() => setConfirmModalAction('confirmReceivedByBuyer')}
                        disabled={purchaseRequestBusy}
                        className={primaryActionClassName}
                      >
                        {purchaseRequestBusy ? '處理中...' : '已收到商品'}
                      </Button>
                    ) : null}
                    {isCompleted ? (
                      <Link
                        to={`/purchase-requests/${purchaseRequest.id}/review`}
                        className={`inline-flex items-center justify-center rounded-xl ${neutralActionClassName}`}
                      >
                        前往評價
                      </Link>
                    ) : null}
                  </div>
                </div>
                {isPending && isSeller ? (
                  <div className="mt-3 grid grid-cols-2 gap-2">
                    <Button
                      type="button"
                      onClick={() => void handlePurchaseRequestAction('accept')}
                      disabled={purchaseRequestBusy}
                      className={`${primaryActionClassName} w-full`}
                    >
                      {purchaseRequestBusy ? '處理中...' : '同意交易'}
                    </Button>
                    <Button
                      type="button"
                      onClick={() => void handlePurchaseRequestAction('reject')}
                      disabled={purchaseRequestBusy}
                      className={`${dangerActionClassName} w-full`}
                    >
                      {purchaseRequestBusy ? '處理中...' : '拒絕交易'}
                    </Button>
                  </div>
                ) : null}
                {isPending && remainingSeconds != null ? (
                  <div className="mt-2 space-y-2">
                    {isSeller ? (
                      <p className="text-base leading-relaxed text-white/90 md:text-sm">
                        請在時限內回覆買家；逾時會記 1 次「回覆失敗」，累積 3 次將暫停刊登。
                        （剩餘時間：{formatCountdown(remainingSeconds)}）
                      </p>
                    ) : null}
                    {isBuyer ? (
                      <p className="text-base leading-relaxed text-white/90 md:text-sm">
                        賣家需在時限內回覆；逾時會記 1 次「回覆失敗」，累積 3 次將暫停刊登。（剩餘時間：
                        {formatCountdown(remainingSeconds)}）
                      </p>
                    ) : null}
                  </div>
                ) : null}
                {isAccepted ? (
                  <div className="mt-3">
                    {!isSeller ? <p className="text-base text-white/90 md:text-sm">等待賣家完成商品交易</p> : null}
                  </div>
                ) : null}
                {isSellerMarkedCompleted ? (
                  <div className="mt-3">
                    {!isBuyer ? <p className="text-base text-white/90 md:text-sm">等待買家確認收貨</p> : null}
                  </div>
                ) : null}
              </div>
            ) : (
              <div className="mt-3 flex flex-wrap items-center justify-between gap-3">
                <p className="text-lg text-white/85 md:text-sm">目前尚未建立交易請求，可先透過聊天與對方溝通。</p>
                {showQuickPurchaseButton ? (
                  <Button
                    type="button"
                    onClick={openPurchaseConfirm}
                    disabled={purchaseRequestCreating}
                    variant="secondary"
                    className={purchaseButtonClassName}
                  >
                    {purchaseRequestCreating ? <span className="font-extrabold">送出中...</span> : '購買'}
                  </Button>
                ) : null}
              </div>
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
                      {formatTaipeiTime(message.createdAt)}
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
      {confirmModalCopy ? (
        <TradeActionConfirmModal
          open={Boolean(confirmModalCopy)}
          busy={purchaseRequestBusy}
          title={confirmModalCopy.title}
          message={confirmModalCopy.message}
          finalConfirmLabel={confirmModalCopy.finalConfirmLabel}
          onClose={() => setConfirmModalAction(null)}
          onConfirm={() => {
            const action = confirmModalAction
            setConfirmModalAction(null)
            if (action) {
              void handlePurchaseRequestAction(action)
            }
          }}
        />
      ) : null}
      <PurchaseConfirmModal
        open={purchaseConfirmOpen}
        listingTitle={listingDetail?.title ?? listingTitle ?? ''}
        busy={purchaseRequestCreating}
        onClose={() => setPurchaseConfirmOpen(false)}
        onConfirm={() => void handleCreatePurchaseRequest()}
      />
    </main>
  )
}
