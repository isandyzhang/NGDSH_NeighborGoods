import { useEffect, useMemo, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import {
  clearListingSharePending,
  resolveListingShareParams,
} from '@/features/listings/listingShareSession'
import { shareListingToLineFlexOnly } from '@/features/listings/utils/lineShare'
import { Button } from '@/shared/ui/Button'

type SharePhase = 'loading' | 'done' | 'cancelled' | 'error'

export const ListingShareLiffPage = () => {
  const navigate = useNavigate()
  const location = useLocation()
  const [phase, setPhase] = useState<SharePhase>('loading')
  const [message, setMessage] = useState('準備分享中…')

  const shareParams = useMemo(
    () => resolveListingShareParams(location.search),
    [location.search],
  )

  const returnTo = shareParams?.returnTo ?? '/listings'

  useEffect(() => {
    let disposed = false

    void (async () => {
      if (!shareParams) {
        if (!disposed) {
          setPhase('error')
          setMessage('缺少分享參數，請返回商品頁重試。')
        }
        return
      }

      const result = await shareListingToLineFlexOnly(
        {
          listingId: shareParams.listingId,
          listingTitle: shareParams.listingTitle,
          priceLabel: shareParams.priceLabel,
          categoryName: shareParams.categoryName,
          conditionName: shareParams.conditionName,
          imageUrl: shareParams.imageUrl,
          residenceName: shareParams.residenceName,
        },
        shareParams.returnTo,
      )

      if (disposed) {
        return
      }

      if (result.reason === 'SENT') {
        clearListingSharePending()
        setPhase('done')
        setMessage('已送出 Flex Message。')
        return
      }
      if (result.reason === 'USER_CANCELLED_OR_CLOSED') {
        setPhase('cancelled')
        setMessage('你已取消分享。')
        return
      }
      if (result.reason === 'NOT_LOGGED_IN') {
        setPhase('loading')
        setMessage(result.errorMessage ?? '正在導向 LINE 登入…')
        return
      }
      if (result.reason === 'NOT_IN_LINE_CLIENT' || result.reason === 'LIFF_UNAVAILABLE') {
        setPhase('error')
        setMessage('目前不是可分享 Flex 的 LIFF 環境，請在 LINE App 內開啟。')
        return
      }
      if (result.reason === 'SHARE_TARGET_PICKER_UNAVAILABLE') {
        setPhase('error')
        setMessage('目前 LIFF 環境不支援 shareTargetPicker。')
        return
      }
      setPhase('error')
      const href = typeof window !== 'undefined' ? window.location.href : ''
      setMessage(
        `分享失敗：${[result.errorCode, result.errorMessage, result.contextType].filter(Boolean).join(' | ') || '未知錯誤'}${href ? `\n\n網址：${href}` : ''}`,
      )
    })()

    return () => {
      disposed = true
    }
  }, [shareParams])

  return (
    <main className="mx-auto flex min-h-[50vh] max-w-md flex-col justify-center gap-4 px-4 py-8">
      <h1 className="text-xl font-semibold text-text-main">LINE 商品分享</h1>
      <p className="whitespace-pre-wrap break-all text-sm text-text-subtle">{message}</p>
      <div className="grid grid-cols-1 gap-2">
        <Button type="button" variant="secondary" onClick={() => navigate(returnTo)}>
          返回商品頁
        </Button>
        {phase === 'error' ? (
          <Button type="button" onClick={() => window.location.reload()}>
            重試分享
          </Button>
        ) : null}
      </div>
    </main>
  )
}
