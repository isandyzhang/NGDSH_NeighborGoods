import { useCallback, useEffect, useState } from 'react'
import {
  adminApi,
  type AdminPickupLocation,
  type AdminResidence,
  type UpsertAdminPickupLocationPayload,
} from '@/features/admin/api/adminApi'
import { adminLookupFieldClassName } from '@/features/admin/constants/adminLookupStyles'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'

const SHARED_SCOPE = 'shared'

const emptyForm = (): Omit<UpsertAdminPickupLocationPayload, 'residenceId'> => ({
  displayName: '',
  sortOrder: 0,
  isActive: true,
})

export const AdminPickupLocationsCard = () => {
  const [residences, setResidences] = useState<AdminResidence[]>([])
  const [scope, setScope] = useState<string>(SHARED_SCOPE)
  const [items, setItems] = useState<AdminPickupLocation[]>([])
  const [form, setForm] = useState(emptyForm)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  const residenceId = scope === SHARED_SCOPE ? null : Number(scope)

  const loadResidences = useCallback(async () => {
    setResidences(await adminApi.listResidences())
  }, [])

  const loadItems = useCallback(async () => {
    const rows = await adminApi.listPickupLocations(
      residenceId === null ? { sharedOnly: true } : { residenceId },
    )
    setItems(rows)
  }, [residenceId])

  useEffect(() => {
    let disposed = false
    void loadResidences().catch((err: unknown) => {
      if (!disposed) {
        setMessage(err instanceof ApiClientError ? err.message : '讀取社宅失敗')
      }
    })
    return () => {
      disposed = true
    }
  }, [loadResidences])

  useEffect(() => {
    let disposed = false
    setLoading(true)
    void loadItems()
      .catch((err: unknown) => {
        if (!disposed) {
          setMessage(err instanceof ApiClientError ? err.message : '讀取面交地點失敗')
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
  }, [loadItems])

  const payload = (): UpsertAdminPickupLocationPayload => ({
    displayName: form.displayName.trim(),
    residenceId,
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
        await adminApi.updatePickupLocation(editingId, payload())
        setMessage('面交地點已更新')
      } else {
        await adminApi.createPickupLocation(payload())
        setMessage('面交地點已新增')
      }
      setForm(emptyForm())
      setEditingId(null)
      await loadItems()
    } catch (err) {
      setMessage(err instanceof ApiClientError ? err.message : '儲存面交地點失敗')
    } finally {
      setSaving(false)
    }
  }

  const handleEdit = (item: AdminPickupLocation) => {
    setEditingId(item.id)
    setForm({
      displayName: item.displayName,
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

  const handleToggleEnabled = async (item: AdminPickupLocation) => {
    setSaving(true)
    setMessage(null)
    try {
      await adminApi.setPickupLocationEnabled(item.id, !item.isActive)
      await loadItems()
      setMessage(item.isActive ? '面交地點已停用' : '面交地點已啟用')
    } catch (err) {
      setMessage(err instanceof ApiClientError ? err.message : '更新狀態失敗')
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (item: AdminPickupLocation) => {
    if (item.isProtected || !window.confirm(`確定刪除面交地點「${item.displayName}」？`)) {
      return
    }
    setSaving(true)
    setMessage(null)
    try {
      await adminApi.deletePickupLocation(item.id)
      if (editingId === item.id) {
        handleCancelEdit()
      }
      await loadItems()
      setMessage('面交地點已刪除')
    } catch (err) {
      setMessage(err instanceof ApiClientError ? err.message : '刪除面交地點失敗')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Card>
      <h2 className="text-xl font-semibold text-text-main">面交地點</h2>
      <p className="mt-1 text-sm text-text-subtle">每個社宅各自維護面交點；「私訊」放在全站共用。</p>

      <div className="mt-4 space-y-3">
        <select
          value={scope}
          onChange={(event) => {
            setScope(event.target.value)
            handleCancelEdit()
          }}
          className={adminLookupFieldClassName}
        >
          <option value={SHARED_SCOPE}>全站共用</option>
          {residences
            .filter((item) => item.id !== 0)
            .map((item) => (
              <option key={item.id} value={String(item.id)}>
                {item.displayName}
              </option>
            ))}
        </select>
        <input
          value={form.displayName}
          onChange={(event) => setForm((current) => ({ ...current, displayName: event.target.value }))}
          placeholder="面交地點名稱（例如：北棟管理室）"
          maxLength={128}
          className={adminLookupFieldClassName}
        />
        <div className="grid gap-3 md:grid-cols-2">
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
        <div className="flex flex-wrap gap-2">
          <Button type="button" onClick={() => void handleSave()} disabled={saving || !form.displayName.trim()}>
            {editingId !== null ? '更新地點' : '新增地點'}
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
          <p className="text-sm text-text-subtle">載入面交地點中...</p>
        ) : items.length ? (
          items.map((item) => (
            <div key={item.id} className="rounded-xl border border-border bg-surface p-3">
              <p className="font-medium text-text-main">{item.displayName}</p>
              <p className="mt-1 text-sm text-text-subtle">
                {item.residenceName ?? '全站共用'}・排序 {item.sortOrder}・{item.isActive ? '啟用' : '停用'}・商品{' '}
                {item.listingCount}
                {item.isProtected ? '・系統預設' : ''}
              </p>
              <div className="mt-2 flex flex-wrap gap-2">
                <Button type="button" variant="secondary" onClick={() => handleEdit(item)} disabled={saving}>
                  編輯
                </Button>
                <Button type="button" variant="secondary" onClick={() => void handleToggleEnabled(item)} disabled={saving}>
                  {item.isActive ? '停用' : '啟用'}
                </Button>
                {!item.isProtected ? (
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
          <p className="text-sm text-text-subtle">此範圍尚無面交地點</p>
        )}
      </div>
    </Card>
  )
}
