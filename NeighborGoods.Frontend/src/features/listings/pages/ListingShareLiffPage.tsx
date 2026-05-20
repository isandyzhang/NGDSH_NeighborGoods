import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { shareListingToLineFlexOnly } from '@/features/listings/utils/lineShare'
import { Button } from '@/shared/ui/Button'

type SharePhase = 'loading' | 'done' | 'cancelled' | 'error'

const safeReturnPath = (value: string | null, fallback: string) => {
  if (!value) {
    return fallback
  }
  return value.startsWith('/') ? value : fallback
}

export const ListingShareLiffPage = () => {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const [phase, setPhase] = useState<SharePhase>('loading')
  const [message, setMessage] = useState('準備分享中...')

  const listingId = searchParams.get('listingId') ?? ''
  const listingTitle = searchParams.get('title') ?? ''
  const priceLabel = searchParams.get('price') ?? undefined
  const categoryName = searchParams.get('category') ?? undefined
  const conditionName = searchParams.get('condition') ?? undefined
  const returnTo = useMemo(
    () => safeReturnPath(searchParams.get('returnTo'), listingId ? `/listings/${listingId}` : '/listings'),
    [listingId, searchParams]
  )

  useEffect(() => {
    let disposed = false
    void (async () => {
      if (!listingId || !listingTitle.trim()) {
        if (!disposed) {
          setPhase('error')
          setMessage('缺少分享參數，請返回商品頁重試。')
        }
        return
      }

      const result = await shareListingToLineFlexOnly({
        listingId,
        listingTitle,
        priceLabel,
        categoryName,
        conditionName,
      })
      if (disposed) {
        return
      }

      if (result.reason === 'SENT') {
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
        setPhase('error')
        setMessage('LIFF 尚未登入，請先完成 LINE 登入後再試。')
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
      setMessage(
        `分享失敗：${[result.errorCode, result.errorMessage, result.contextType].filter(Boolean).join(' | ') || '未知錯誤'}`
      )
    })()

    return () => {
      disposed = true
    }
  }, [categoryName, conditionName, listingId, listingTitle, priceLabel])

  return (
    <main className="mx-auto flex min-h-[50vh] max-w-md flex-col justify-center gap-4 px-4 py-8">
      <h1 className="text-xl font-semibold text-text-main">LINE 商品分享</h1>
      <p className="text-sm text-text-subtle">{message}</p>
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
