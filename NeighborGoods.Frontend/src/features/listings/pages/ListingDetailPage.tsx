import { useEffect, useMemo, useState } from 'react'
import { motion } from 'framer-motion'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { listingApi, type ListingDetail } from '@/features/listings/api/listingApi'
import { PurchaseConfirmModal } from '@/features/listings/components/PurchaseConfirmModal'
import { useAuth } from '@/features/auth/components/AuthProvider'
import { messagingApi } from '@/features/messaging/api/messagingApi'
import { ApiClientError } from '@/shared/types/api'
import { Button, getButtonClassName } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { EmptyState } from '@/shared/ui/EmptyState'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

const formatPrice = (item: ListingDetail) => {
  if (item.isFree) {
    return '免費'
  }

  return `NT$ ${item.price.toLocaleString()}`
}

const formatCountdown = (seconds: number) => {
  const normalized = Math.max(0, Math.floor(seconds))
  const hours = Math.floor(normalized / 3600)
  const minutes = Math.floor((normalized % 3600) / 60)
  const remainingSeconds = normalized % 60
  return [hours, minutes, remainingSeconds].map((value) => value.toString().padStart(2, '0')).join(':')
}

export const ListingDetailPage = () => {
  const navigate = useNavigate()
  const { isAuthenticated, tokens } = useAuth()
  const { id = '' } = useParams()
  const [searchParams] = useSearchParams()
  const [item, setItem] = useState<ListingDetail | null>(null)
  const [countdownNowMs, setCountdownNowMs] = useState(() => Date.now())
  const [loading, setLoading] = useState(true)
  const [conversationBusy, setConversationBusy] = useState(false)
  const [purchaseBusy, setPurchaseBusy] = useState(false)
  const [purchaseConfirmOpen, setPurchaseConfirmOpen] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!id) {
      return
    }

    let disposed = false
    setLoading(true)
    setError(null)

    void listingApi
      .getById(id)
      .then((data) => {
        if (!disposed) {
          setItem(data)
        }
      })
      .catch((err: unknown) => {
        if (disposed) {
          return
        }

        const message = err instanceof ApiClientError ? err.message : '讀取商品詳情失敗'
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
  }, [id])

  useEffect(() => {
    if (!item?.pendingPurchaseRequestExpireAt) {
      return
    }

    const timer = window.setInterval(() => {
      setCountdownNowMs(Date.now())
    }, 1000)

    return () => {
      window.clearInterval(timer)
    }
  }, [item?.pendingPurchaseRequestExpireAt])

  const pendingRemainingSeconds = !item?.pendingPurchaseRequestExpireAt
    ? null
    : Math.max(0, Math.floor((new Date(item.pendingPurchaseRequestExpireAt).getTime() - countdownNowMs) / 1000))
  const hasPendingPurchaseRequest = pendingRemainingSeconds != null && pendingRemainingSeconds > 0
  const isOwnListing = !!item && tokens?.userId === item.seller.id
  const source = searchParams.get('from')
  const sourceConversationId = searchParams.get('conversationId')
  const backTarget = useMemo(() => {
    if (source === 'chat' && sourceConversationId) {
      return {
        to: `/messages/${sourceConversationId}`,
        label: '← 返回對話視窗',
      }
    }

    if (source === 'create' || source === 'edit') {
      return {
        to: '/my-listings',
        label: '← 返回我的商品',
      }
    }

    return {
      to: '/listings',
      label: '← 返回商品列表',
    }
  }, [source, sourceConversationId])

  const handleChat = async () => {
    if (!item || conversationBusy) {
      return
    }
    if (!isAuthenticated) {
      navigate('/login')
      return
    }
    if (isOwnListing) {
      setError('這是你的商品，無法與自己建立對話')
      return
    }

    setConversationBusy(true)
    setError(null)
    try {
      const conversationId = await messagingApi.ensureConversation(item.id, item.seller.id)
      navigate(`/messages/${conversationId}`)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '建立對話失敗')
    } finally {
      setConversationBusy(false)
    }
  }

  const openPurchaseConfirm = () => {
    if (!item) {
      return
    }
    if (!isAuthenticated) {
      navigate('/login')
      return
    }
    if (isOwnListing) {
      setError('這是你的商品，無法購買自己的商品')
      return
    }
    setPurchaseConfirmOpen(true)
  }

  const handlePurchase = async () => {
    if (!item || purchaseBusy) {
      return
    }

    setPurchaseBusy(true)
    setError(null)
    try {
      const request = await listingApi.createPurchaseRequest(item.id)
      setPurchaseConfirmOpen(false)
      navigate(`/messages/${request.conversationId}`)
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '送出購買請求失敗')
    } finally {
      setPurchaseBusy(false)
    }
  }

  return (
    <main className="mx-auto w-full max-w-7xl px-4 py-6 md:py-8">
      <Link to={backTarget.to} className="text-lg font-medium text-text-subtle hover:text-text-main">
        {backTarget.label}
      </Link>

      {loading ? <PageSkeleton className="mt-4 h-80" /> : null}
      {error ? <ErrorState description={error} /> : null}

      {!loading && !error && !item ? (
        <div className="mt-4">
          <EmptyState title="查無商品" description="這筆商品可能已下架或不存在。" />
        </div>
      ) : null}

      {item ? (
        <motion.section
          className="mt-4"
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.28, ease: [0.22, 1, 0.36, 1] }}
        >
          <div className="grid gap-4 lg:grid-cols-[minmax(0,0.76fr)_minmax(0,1.24fr)] lg:items-start">
            <motion.div
              initial={{ opacity: 0, x: -14 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ duration: 0.28, delay: 0.05, ease: [0.22, 1, 0.36, 1] }}
            >
            <Card className="space-y-4 !border-[3px] !border-[#6E4F34] !bg-[#F5EBDD] p-4 text-text-main md:p-5">
              <section className="space-y-3">
                <div className="flex items-start justify-between gap-3">
                  <div className="space-y-2">
                    <p className="text-3xl font-bold text-text-main md:text-4xl">{item.seller.displayName || '未提供'}</p>
                    <p className="text-base text-text-subtle md:text-lg">加入時間：{item.seller.memberDays} 天</p>
                  </div>
                  <Link
                    to={`/seller/${item.seller.id}`}
                    className="inline-flex shrink-0 items-center rounded-full border border-[#6E4F34] bg-[#EADBC8] px-3 py-1.5 text-sm font-semibold text-[#4A3423] transition hover:bg-[#E1CFB8]"
                  >
                    查看賣家頁面
                  </Link>
                </div>
                <div className="space-y-2">
                  <div className="flex flex-wrap gap-2 pt-1 text-sm font-semibold md:text-base">
                    <span className={`rounded-full px-2.5 py-1 ${item.seller.emailVerified ? 'bg-[#E3F6EC] text-[#2F7D4E]' : 'bg-surface text-text-muted'}`}>
                      {item.seller.emailVerified ? 'Email 已驗證' : 'Email 未驗證'}
                    </span>
                    <span className={`rounded-full px-2.5 py-1 ${item.seller.quickResponder ? 'bg-[#EFE9FF] text-[#5E5AB5]' : 'bg-surface text-text-muted'}`}>
                      {item.seller.quickResponder ? '快速回覆' : '一般回覆'}
                    </span>
                    <span className={`rounded-full px-2.5 py-1 ${item.seller.lineBound ? 'bg-[#E5F5E9] text-[#1F9D4D]' : 'bg-surface text-text-muted'}`}>
                      {item.seller.lineBound ? 'LINE 已綁定' : 'LINE 未綁定'}
                    </span>
                  </div>
                </div>
              </section>
            </Card>
            </motion.div>

            <motion.div
              initial={{ opacity: 0, x: 14 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ duration: 0.28, delay: 0.1, ease: [0.22, 1, 0.36, 1] }}
            >
            <Card className="space-y-4 border-border bg-surface p-4 md:p-5 lg:p-4">
              <section className="space-y-4">
                <h2 className="text-2xl font-bold text-text-main md:text-3xl lg:text-2xl">商品資訊</h2>
                <div className="space-y-4 lg:flex lg:items-start lg:gap-4 lg:space-y-0">
                  <div className="relative overflow-hidden rounded-2xl border border-border bg-surface-2 lg:w-[44%] lg:shrink-0">
                    {item.mainImageUrl ? (
                      <img src={item.mainImageUrl} alt={item.title} className="aspect-[16/10] w-full object-cover lg:aspect-[4/3]" />
                    ) : (
                      <div className="flex aspect-[16/10] items-center justify-center text-xl text-text-muted lg:aspect-[4/3]">無圖片</div>
                    )}
                    {hasPendingPurchaseRequest ? (
                      <div className="absolute inset-0 z-20 flex flex-col items-center justify-center gap-2 bg-black/55 px-4 text-center text-white">
                        <p className="text-xl font-semibold tracking-wide">交易處理中</p>
                        <p className="text-3xl font-bold tabular-nums">{formatCountdown(pendingRemainingSeconds)}</p>
                      </div>
                    ) : null}
                  </div>

                  <div className="space-y-4 lg:w-[56%]">
                    <h3 className="text-3xl font-bold leading-tight text-text-main md:text-5xl lg:text-4xl">{item.title}</h3>
                    <div className="flex flex-wrap items-center gap-3">
                      {item.isFree ? (
                        <span className="inline-flex items-center rounded-full bg-[#2f7d4e] px-5 py-1.5 text-xl font-bold text-white md:text-2xl lg:text-xl">
                          免費
                        </span>
                      ) : (
                        <span className="text-3xl font-bold text-text-main md:text-4xl lg:text-3xl">{formatPrice(item)}</span>
                      )}
                    </div>
                    <p className="text-xl font-medium text-text-subtle md:text-2xl lg:text-xl">
                      {item.categoryName}・{item.conditionName}・{item.residenceName}
                    </p>
                    <p className="text-xl font-medium text-text-subtle md:text-2xl lg:text-xl">面交地點：{item.pickupLocationName}</p>
                    <p className="whitespace-pre-wrap text-lg leading-8 text-text-main md:text-2xl md:leading-9 lg:text-lg lg:leading-7">
                      {item.description || '賣家尚未提供描述。'}
                    </p>
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-3 pt-2">
                  {isOwnListing ? (
                    <Link
                      to={`/listings/${item.id}/edit`}
                      className={getButtonClassName({
                        className: 'col-span-2 inline-flex min-h-[3.2rem] items-center justify-center text-xl font-semibold md:text-2xl',
                      })}
                    >
                      修改商品
                    </Link>
                  ) : (
                    <>
                      <Button
                        type="button"
                        onClick={() => void handleChat()}
                        disabled={conversationBusy}
                        variant="secondary"
                        className="min-h-[3.2rem] text-xl font-semibold md:text-2xl"
                      >
                        {conversationBusy ? '連線中...' : '聊一下'}
                      </Button>
                      <Button
                        type="button"
                        onClick={openPurchaseConfirm}
                        disabled={purchaseBusy}
                        className="min-h-[3.2rem] text-xl font-semibold md:text-2xl"
                      >
                        {purchaseBusy ? '處理中...' : '購買'}
                      </Button>
                    </>
                  )}
                </div>
              </section>
            </Card>
            </motion.div>
          </div>
        </motion.section>
      ) : null}
      <PurchaseConfirmModal
        open={purchaseConfirmOpen}
        listingTitle={item?.title ?? ''}
        busy={purchaseBusy}
        onClose={() => setPurchaseConfirmOpen(false)}
        onConfirm={() => void handlePurchase()}
      />
    </main>
  )
}
