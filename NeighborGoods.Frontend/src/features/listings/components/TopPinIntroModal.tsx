import { useEffect, useState } from 'react'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'

type TopPinIntroModalProps = {
  open: boolean
  onClose: () => void
  onConfirmTopPin: (skipNextReminder: boolean) => void
  onGoSubmission: () => void
}

export const TopPinIntroModal = ({
  open,
  onClose,
  onConfirmTopPin,
  onGoSubmission,
}: TopPinIntroModalProps) => {
  const [skipNextReminder, setSkipNextReminder] = useState(false)

  useEffect(() => {
    if (!open) {
      setSkipNextReminder(false)
    }
  }, [open])

  if (!open) {
    return null
  }

  return (
    <div className="fixed inset-0 z-40 flex items-center justify-center bg-black/50 px-4" role="dialog" aria-modal="true">
      <button type="button" className="absolute inset-0" aria-label="關閉置頂說明" onClick={onClose} />
      <Card className="relative z-10 w-full max-w-xl space-y-4 rounded-2xl p-5">
        <h2 className="text-2xl font-bold text-text-main">讓更多人優先看到你的商品</h2>
        <div className="space-y-2 text-base leading-relaxed text-text-subtle">
          <p>商品置頂後會優先顯示在清單前段，提高曝光與成交機會。</p>
          <p>每次使用 1 次置頂，可維持 7 天；期間系統會依規則進行曝光排序。</p>
          <p>通過投稿審核後，平台可依商品類別安排 LINE OA 推播，進一步增加觸及。</p>
          <p>你可以透過活動、任務或官方發放獲得置頂次數。</p>
        </div>

        <label className="inline-flex items-center gap-2 text-sm text-text-main">
          <input
            type="checkbox"
            className="h-4 w-4 rounded border-border"
            checked={skipNextReminder}
            onChange={(event) => setSkipNextReminder(event.target.checked)}
          />
          下次不再提醒
        </label>

        <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
          <Button type="button" onClick={() => onConfirmTopPin(skipNextReminder)} className="min-h-[2.9rem] font-semibold">
            馬上置頂
          </Button>
          <Button
            type="button"
            variant="secondary"
            onClick={onGoSubmission}
            className="min-h-[2.9rem] font-semibold"
          >
            我要投稿
          </Button>
        </div>
      </Card>
    </div>
  )
}
