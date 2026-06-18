import { useState } from 'react'
import { listingApi } from '@/features/listings/api/listingApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { ConfirmModal } from '@/shared/ui/modal/ConfirmModal'

const SOLD_CONFIRM_MESSAGE =
  '此商品有相關的對話記錄，建議您透過正常交易流程完成交易，這樣可以建立買賣雙方關聯並進行評價。您確定要直接標記為交易完成嗎？'

type Props = {
  listingId: string
  disabled?: boolean
  onCompleted?: () => void | Promise<void>
  className?: string
}

export const ListingExpiredActionPanel = ({
  listingId,
  disabled = false,
  onCompleted,
  className = '',
}: Props) => {
  const [renewBusy, setRenewBusy] = useState(false)
  const [soldBusy, setSoldBusy] = useState(false)
  const [soldConfirmOpen, setSoldConfirmOpen] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleRenew = async () => {
    setRenewBusy(true)
    setError(null)
    try {
      await listingApi.renew(listingId)
      await onCompleted?.()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '延續刊登失敗')
    } finally {
      setRenewBusy(false)
    }
  }

  const handleMarkSold = async () => {
    setSoldBusy(true)
    setError(null)
    try {
      const result = await listingApi.markSoldFromExpiry(listingId)
      setSoldConfirmOpen(false)
      if (result.warning) {
        window.alert(result.warning)
      }
      await onCompleted?.()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '標記已成交失敗')
    } finally {
      setSoldBusy(false)
    }
  }

  return (
    <div className={`rounded-2xl border border-amber-200 bg-amber-50/90 p-4 md:p-5 ${className}`}>
      <p className="text-base font-semibold text-text-main md:text-lg">此商品已刊登滿 14 天，目前為非活躍狀態</p>
      <p className="mt-1 text-sm text-text-subtle md:text-base">
        請選擇延續刊登以重新曝光，或若已成交請更新商品狀態。
      </p>
      <div className="mt-4 flex flex-col gap-3 sm:flex-row">
        <Button
          type="button"
          onClick={() => void handleRenew()}
          disabled={disabled || renewBusy || soldBusy}
          className="min-h-[2.75rem] flex-1 text-base font-semibold"
        >
          {renewBusy ? '處理中...' : '延續刊登'}
        </Button>
        <div className="flex flex-1 flex-col gap-1">
          <Button
            type="button"
            variant="secondary"
            onClick={() => setSoldConfirmOpen(true)}
            disabled={disabled || renewBusy || soldBusy}
            className="min-h-[2.75rem] w-full text-base font-semibold"
          >
            {soldBusy ? '處理中...' : '已經成交了！恭喜'}
          </Button>
          <p className="text-center text-xs text-text-muted sm:text-left">請更新商品狀態，避免買家撲空</p>
        </div>
      </div>
      {error ? <p className="mt-3 text-sm text-red-600">{error}</p> : null}

      <ConfirmModal
        open={soldConfirmOpen}
        busy={soldBusy}
        onClose={() => setSoldConfirmOpen(false)}
        title="標記為已成交"
        message={`${SOLD_CONFIRM_MESSAGE} 請更新商品狀態，避免買家撲空。`}
        confirmLabel="確認已成交"
        cancelLabel="取消"
        onConfirm={() => void handleMarkSold()}
      />
    </div>
  )
}
