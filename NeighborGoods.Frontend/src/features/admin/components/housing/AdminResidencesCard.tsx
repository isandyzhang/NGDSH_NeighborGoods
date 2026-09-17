import { useCallback, useEffect, useState } from 'react'
import {
  adminApi,
  type AdminResidence,
  type UpsertAdminResidencePayload,
} from '@/features/admin/api/adminApi'
import { adminLookupFieldClassName } from '@/features/admin/constants/adminLookupStyles'
import { TAIWAN_CITIES } from '@/features/admin/constants/taiwanCities'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'

const emptyForm = (): UpsertAdminResidencePayload => ({
  displayName: '',
  city: null,
  district: '',
  notes: '',
  sortOrder: 0,
  isActive: true,
})

const UNKNOWN_RESIDENCE_ID = 0

export const AdminResidencesCard = () => {
  const [items, setItems] = useState<AdminResidence[]>([])
  const [form, setForm] = useState(emptyForm)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  const load = useCallback(async () => {
    setItems(await adminApi.listResidences())
  }, [])

  useEffect(() => {
    let disposed = false
    setLoading(true)
    void load()
      .catch((err: unknown) => {
        if (!disposed) {
          setMessage(err instanceof ApiClientError ? err.message : '讀取社宅失敗')
        }
      })
      .finally(() => {
        if (!disposed) {
          setLoading(false)
        }
      })
    return () => {
      disposed = true
    }
  }, [load])

  const payload = (): UpsertAdminResidencePayload => ({
    displayName: form.displayName.trim(),
    city: form.city,
    district: form.district?.trim() || null,
    notes: form.notes?.trim() || null,
    sortOrder: form.sortOrder,
    isActive: form.isActive,
  })

  const handleSave = async () => {
    if (saving || !form.displayName.trim()) {
      return
    }
    setSaving(true)
    setMessage(null)
    try {
      if (editingId !== null) {
        await adminApi.updateResidence(editingId, payload())
        setMessage('社宅已更新')
      } else {
        await adminApi.createResidence(payload())
        setMessage('社宅已新增')
      }
      setForm(emptyForm())
      setEditingId(null)
      await load()
    } catch (err) {
      setMessage(err instanceof ApiClientError ? err.message : '儲存社宅失敗')
    } finally {
      setSaving(false)
    }
  }

  const handleEdit = (item: AdminResidence) => {
    setEditingId(item.id)
    setForm({
      displayName: item.displayName,
      city: item.city,
      district: item.district ?? '',
      notes: item.notes ?? '',
      sortOrder: item.sortOrder,
      isActive: item.isActive,
    })
    setMessage(null)
  }

  const handleCancelEdit = () => {
    setEditingId(null)
    setForm(emptyForm())
    setMessage(null)
  }

  const handleToggleEnabled = async (item: AdminResidence) => {
    setSaving(true)
    setMessage(null)
    try {
      await adminApi.setResidenceEnabled(item.id, !item.isActive)
      await load()
      setMessage(item.isActive ? '社宅已停用' : '社宅已啟用')
    } catch (err) {
      setMessage(err instanceof ApiClientError ? err.message : '更新狀態失敗')
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (item: AdminResidence) => {
    if (item.id === UNKNOWN_RESIDENCE_ID || !window.confirm(`確定刪除社宅「${item.displayName}」？`)) {
      return
    }
    setSaving(true)
    setMessage(null)
    try {
      await adminApi.deleteResidence(item.id)
      if (editingId === item.id) {
        handleCancelEdit()
      }
      await load()
      setMessage('社宅已刪除')
    } catch (err) {
      setMessage(err instanceof ApiClientError ? err.message : '刪除社宅失敗')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Card>
      <h2 className="text-xl font-semibold text-text-main">社宅清單</h2>
      <p className="mt-1 text-sm text-text-subtle">新增其他社宅後，前台刊登與篩選就會出現該選項。</p>

      <div className="mt-4 space-y-3">
        <input
          value={form.displayName}
          onChange={(event) => setForm((current) => ({ ...current, displayName: event.target.value }))}
          placeholder="社宅名稱（例如：東明）"
          maxLength={128}
          className={adminLookupFieldClassName}
        />
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <select
            value={form.city ?? ''}
            onChange={(event) => setForm((current) => ({ ...current, city: event.target.value || null }))}
            className={adminLookupFieldClassName}
          >
            <option value="">縣市（可選）</option>
            {TAIWAN_CITIES.map((city) => (
              <option key={city} value={city}>
                {city}
              </option>
            ))}
          </select>
          <input
            value={form.district ?? ''}
            onChange={(event) => setForm((current) => ({ ...current, district: event.target.value }))}
            placeholder="行政區（可選）"
            maxLength={32}
            className={adminLookupFieldClassName}
          />
          <input
            type="number"
            value={form.sortOrder}
            onChange={(event) => setForm((current) => ({ ...current, sortOrder: Number(event.target.value) }))}
            className={adminLookupFieldClassName}
            placeholder="排序"
          />
          <select
            value={form.isActive ? '1' : '0'}
            onChange={(event) => setForm((current) => ({ ...current, isActive: event.target.value === '1' }))}
            className={adminLookupFieldClassName}
          >
            <option value="1">啟用</option>
            <option value="0">停用</option>
          </select>
        </div>
        <textarea
          value={form.notes ?? ''}
          onChange={(event) => setForm((current) => ({ ...current, notes: event.target.value }))}
          placeholder="管理員備註（前台不顯示）"
          rows={2}
          maxLength={500}
          className={adminLookupFieldClassName}
        />
        <div className="flex flex-wrap gap-2">
          <Button type="button" onClick={() => void handleSave()} disabled={saving || !form.displayName.trim()}>
            {editingId !== null ? '更新社宅' : '新增社宅'}
          </Button>
          {editingId !== null ? (
            <Button type="button" variant="secondary" onClick={handleCancelEdit} disabled={saving}>
              取消編輯
            </Button>
          ) : null}
        </div>
      </div>

      {message ? <p className="mt-2 text-sm text-text-subtle">{message}</p> : null}

      <div className="mt-4 space-y-2">
        {loading ? (
          <p className="text-sm text-text-subtle">載入社宅中...</p>
        ) : items.length ? (
          items.map((item) => (
            <div key={item.id} className="rounded-xl border border-border bg-surface p-3">
              <p className="font-medium text-text-main">
                {item.displayName}
                {item.city ? `・${item.city}${item.district ?? ''}` : ''}
              </p>
              <p className="mt-1 text-sm text-text-subtle">
                排序 {item.sortOrder}・{item.isActive ? '啟用' : '停用'}・商品 {item.listingCount}・面交點 {item.pickupLocationCount}
              </p>
              {item.notes ? <p className="text-sm text-text-subtle">備註：{item.notes}</p> : null}
              <div className="mt-2 flex flex-wrap gap-2">
                <Button type="button" variant="secondary" onClick={() => handleEdit(item)} disabled={saving}>
                  編輯
                </Button>
                <Button type="button" variant="secondary" onClick={() => void handleToggleEnabled(item)} disabled={saving}>
                  {item.isActive ? '停用' : '啟用'}
                </Button>
                {item.id !== UNKNOWN_RESIDENCE_ID ? (
                  <Button
                    type="button"
                    variant="secondary"
                    onClick={() => void handleDelete(item)}
                    disabled={saving}
                    className="border-[#e9b4b4] bg-[#fbe2e2] text-[#b23a3a] hover:bg-[#f6d3d3]"
                  >
                    刪除
                  </Button>
                ) : null}
              </div>
            </div>
          ))
        ) : (
          <p className="text-sm text-text-subtle">尚無社宅</p>
        )}
      </div>
    </Card>
  )
}
