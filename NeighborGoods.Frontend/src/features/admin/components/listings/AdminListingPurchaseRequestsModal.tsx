import { useEffect, useState } from 'react'
import { adminApi, type AdminPurchaseRequest } from '@/features/admin/api/adminApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { AppModal } from '@/shared/ui/modal/AppModal'

type Props = {
  listingId: string | null
  open: boolean
  onClose: () => void
  onUpdated: () => void
}

const statusOptions = [
  { value: 0, label: '待賣家回覆' },
  { value: 1, label: '已同意' },
  { value: 2, label: '已拒絕' },
  { value: 3, label: '已逾期' },
  { value: 4, label: '已取消' },
  { value: 5, label: '待買家確認收貨' },
  { value: 6, label: '已完成' },
] as const

const statusLabel = (status: number) => statusOptions.find((option) => option.value === status)?.label ?? `狀態 ${status}`

const formatDateTime = (value: string | null) =>
  value
    ? new Date(value).toLocaleString('zh-TW', {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
      })
    : '—'

const inputClassName =
  'w-full rounded-xl border border-border bg-surface px-3 py-2 text-text-main outline-none transition focus:border-brand'

export const AdminListingPurchaseRequestsModal = ({ listingId, open, onClose, onUpdated }: Props) => {
  const [items, setItems] = useState<AdminPurchaseRequest[]>([])
  const [loading, setLoading] = useState(false)
  const [savingId, setSavingId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [draftStatuses, setDraftStatuses] = useState<Record<string, number>>({})

  const load = async () => {
    if (!listingId) return
    setLoading(true)
    setError(null)
    try {
      const result = await adminApi.getListingPurchaseRequests(listingId)
      setItems(result.items)
      setDraftStatuses(Object.fromEntries(result.items.map((item) => [item.id, item.status])))
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '讀取交易紀錄失敗')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (open && listingId) void load()
  }, [open, listingId])

  const handleSave = async (item: AdminPurchaseRequest) => {
    const nextStatus = draftStatuses[item.id] ?? item.status
    if (nextStatus === item.status) return
    const reason = window.prompt('請輸入這次調整交易狀態的原因：')
    if (reason === null) return

    setSavingId(item.id)
    setError(null)
    try {
      await adminApi.updatePurchaseRequestStatus(item.id, nextStatus, reason)
      await load()
      onUpdated()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '更新交易狀態失敗')
    } finally {
      setSavingId(null)
    }
  }

  return (
    <AppModal open={open} onClose={onClose} maxWidthClassName="max-w-4xl">
      <div className="space-y-1">
        <h2 className="text-xl font-semibold text-text-main">商品交易紀錄</h2>
        <p className="text-sm text-text-subtle">調整最新一筆交易時，系統會同步更新商品狀態。</p>
      </div>

      {loading ? <p className="text-sm text-text-subtle">讀取中...</p> : null}
      {error ? <p className="text-sm text-rose-600">{error}</p> : null}

      {!loading && items.length === 0 ? <p className="text-sm text-text-subtle">此商品沒有交易申請紀錄。</p> : null}

      <div className="max-h-[60vh] space-y-3 overflow-y-auto pr-1">
        {items.map((item) => (
          <section key={item.id} className="space-y-3 rounded-xl border border-border bg-surface-2 p-3">
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div>
                <p className="font-semibold text-text-main">
                  買家：{item.buyerDisplayName}
                  {item.isCurrent ? <span className="ml-2 text-sm text-brand">最新交易</span> : null}
                </p>
                <p className="text-sm text-text-subtle">建立：{formatDateTime(item.createdAt)}</p>
                <p className="text-sm text-text-subtle">回覆：{formatDateTime(item.respondedAt)}</p>
              </div>
              <span className="rounded-full border border-border bg-surface px-3 py-1 text-sm text-text-main">
                {statusLabel(item.status)}
              </span>
            </div>

            {item.responseReason ? <p className="text-sm text-text-subtle">原因：{item.responseReason}</p> : null}

            <div className="grid gap-2 sm:grid-cols-[1fr_auto]">
              <select
                className={inputClassName}
                value={draftStatuses[item.id] ?? item.status}
                disabled={!item.isCurrent}
                onChange={(event) =>
                  setDraftStatuses((current) => ({ ...current, [item.id]: Number(event.target.value) }))
                }
              >
                {statusOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.value} {option.label}
                  </option>
                ))}
              </select>
              <Button
                type="button"
                onClick={() => void handleSave(item)}
                disabled={!item.isCurrent || savingId === item.id || (draftStatuses[item.id] ?? item.status) === item.status}
              >
                {savingId === item.id ? '更新中...' : item.isCurrent ? '更新交易狀態' : '歷史紀錄（唯讀）'}
              </Button>
            </div>
          </section>
        ))}
      </div>

      <div className="flex justify-end">
        <Button type="button" variant="secondary" onClick={onClose}>
          關閉
        </Button>
      </div>
    </AppModal>
  )
}
