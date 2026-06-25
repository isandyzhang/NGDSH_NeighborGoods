import { useEffect, useState } from 'react'
import { adminApi, type AdminListingDetail } from '@/features/admin/api/adminApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { AppModal } from '@/shared/ui/modal/AppModal'

type Props = {
  listingId: string | null
  open: boolean
  onClose: () => void
  onUpdated: () => void
}

const inputClassName =
  'w-full rounded-xl border border-border bg-surface px-3 py-2 text-text-main outline-none transition placeholder:text-text-muted focus:border-brand'

export const AdminListingEditModal = ({ listingId, open, onClose, onUpdated }: Props) => {
  const [item, setItem] = useState<AdminListingDetail | null>(null)
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!open || !listingId) {
      return
    }
    setLoading(true)
    setError(null)
    void adminApi
      .getListingDetail(listingId)
      .then((result) => setItem(result))
      .catch((err: unknown) => setError(err instanceof ApiClientError ? err.message : '讀取商品失敗'))
      .finally(() => setLoading(false))
  }, [open, listingId])

  const handleSave = async () => {
    if (!item || !listingId || saving) return
    setSaving(true)
    setError(null)
    try {
      await adminApi.updateListing(listingId, {
        title: item.title,
        description: item.description,
        categoryCode: item.categoryCode,
        conditionCode: item.conditionCode,
        price: item.price,
        residenceCode: item.residenceCode,
        pickupLocationCode: item.pickupLocationCode,
        isFree: item.isFree,
        isCharity: item.isCharity,
        isTradeable: item.isTradeable,
        status: item.status,
      })
      onUpdated()
      onClose()
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '更新商品失敗')
    } finally {
      setSaving(false)
    }
  }

  return (
    <AppModal open={open} onClose={onClose} maxWidthClassName="max-w-2xl">
      <h2 className="text-xl font-semibold text-text-main">編輯商品與狀態</h2>
      {loading ? <p className="text-sm text-text-subtle">載入中...</p> : null}
      {error ? <p className="text-sm text-rose-600">{error}</p> : null}
      {item ? (
        <div className="grid gap-3 md:grid-cols-2">
          <label className="md:col-span-2">
            <span className="text-sm text-text-subtle">標題</span>
            <input className={inputClassName} value={item.title} onChange={(e) => setItem({ ...item, title: e.target.value })} />
          </label>
          <label className="md:col-span-2">
            <span className="text-sm text-text-subtle">描述</span>
            <textarea
              className={inputClassName}
              rows={3}
              value={item.description}
              onChange={(e) => setItem({ ...item, description: e.target.value })}
            />
          </label>
          <label>
            <span className="text-sm text-text-subtle">價格</span>
            <input
              className={inputClassName}
              type="number"
              min={0}
              value={item.price}
              onChange={(e) => setItem({ ...item, price: Number(e.target.value) })}
            />
          </label>
          <label>
            <span className="text-sm text-text-subtle">狀態</span>
            <select className={inputClassName} value={item.status} onChange={(e) => setItem({ ...item, status: Number(e.target.value) })}>
              <option value={0}>0 上架中</option>
              <option value={1}>1 保留</option>
              <option value={2}>2 售出</option>
              <option value={3}>3 已贈與</option>
              <option value={4}>4 已下架</option>
              <option value={5}>5 已易物</option>
            </select>
          </label>
          <label>
            <span className="text-sm text-text-subtle">分類代碼</span>
            <input className={inputClassName} type="number" value={item.categoryCode} onChange={(e) => setItem({ ...item, categoryCode: Number(e.target.value) })} />
          </label>
          <label>
            <span className="text-sm text-text-subtle">成色代碼</span>
            <input className={inputClassName} type="number" value={item.conditionCode} onChange={(e) => setItem({ ...item, conditionCode: Number(e.target.value) })} />
          </label>
          <label>
            <span className="text-sm text-text-subtle">居住區代碼</span>
            <input className={inputClassName} type="number" value={item.residenceCode} onChange={(e) => setItem({ ...item, residenceCode: Number(e.target.value) })} />
          </label>
          <label>
            <span className="text-sm text-text-subtle">取貨地代碼</span>
            <input
              className={inputClassName}
              type="number"
              value={item.pickupLocationCode}
              onChange={(e) => setItem({ ...item, pickupLocationCode: Number(e.target.value) })}
            />
          </label>
          <label className="flex items-center gap-2 text-sm text-text-main">
            <input type="checkbox" checked={item.isFree} onChange={(e) => setItem({ ...item, isFree: e.target.checked })} />
            免費
          </label>
          <label className="flex items-center gap-2 text-sm text-text-main">
            <input type="checkbox" checked={item.isCharity} onChange={(e) => setItem({ ...item, isCharity: e.target.checked })} />
            公益
          </label>
          <label className="flex items-center gap-2 text-sm text-text-main">
            <input type="checkbox" checked={item.isTradeable} onChange={(e) => setItem({ ...item, isTradeable: e.target.checked })} />
            可易物
          </label>
        </div>
      ) : null}
      <div className="flex justify-end gap-2">
        <Button type="button" variant="secondary" onClick={onClose}>
          取消
        </Button>
        <Button type="button" onClick={() => void handleSave()} disabled={!item || saving}>
          儲存
        </Button>
      </div>
    </AppModal>
  )
}
