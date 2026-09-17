import { useCallback, useEffect, useState } from 'react'
import { adminApi, type AdminCategory, type UpsertAdminCategoryPayload } from '@/features/admin/api/adminApi'
import { adminLookupFieldClassName } from '@/features/admin/constants/adminLookupStyles'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'

const emptyForm = (): UpsertAdminCategoryPayload => ({
  displayName: '',
  sortOrder: 0,
  isActive: true,
})

export const AdminCategoriesCard = () => {
  const [items, setItems] = useState<AdminCategory[]>([])
  const [form, setForm] = useState(emptyForm)
  const [editingId, setEditingId] = useState<number | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  const load = useCallback(async () => {
    setItems(await adminApi.listCategories())
  }, [])

  useEffect(() => {
    let disposed = false
    setLoading(true)
    void load()
      .catch((err: unknown) => {
        if (!disposed) {
          setMessage(err instanceof ApiClientError ? err.message : '讀取分類失敗')
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

  const handleSave = async () => {
    if (saving || !form.displayName.trim()) {
      return
    }
    setSaving(true)
    setMessage(null)
    try {
      const payload = {
        displayName: form.displayName.trim(),
        sortOrder: form.sortOrder,
        isActive: form.isActive,
      }
      if (editingId !== null) {
        await adminApi.updateCategory(editingId, payload)
        setMessage('分類已更新')
      } else {
        await adminApi.createCategory(payload)
        setMessage('分類已新增')
      }
      setForm(emptyForm())
      setEditingId(null)
      await load()
    } catch (err) {
      setMessage(err instanceof ApiClientError ? err.message : '儲存分類失敗')
    } finally {
      setSaving(false)
    }
  }

  const handleEdit = (item: AdminCategory) => {
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

  const handleToggleEnabled = async (item: AdminCategory) => {
    setSaving(true)
    setMessage(null)
    try {
      await adminApi.setCategoryEnabled(item.id, !item.isActive)
      await load()
      setMessage(item.isActive ? '分類已停用' : '分類已啟用')
    } catch (err) {
      setMessage(err instanceof ApiClientError ? err.message : '更新狀態失敗')
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (item: AdminCategory) => {
    if (!window.confirm(`確定刪除分類「${item.displayName}」？`)) {
      return
    }
    setSaving(true)
    setMessage(null)
    try {
      await adminApi.deleteCategory(item.id)
      if (editingId === item.id) {
        handleCancelEdit()
      }
      await load()
      setMessage('分類已刪除')
    } catch (err) {
      setMessage(err instanceof ApiClientError ? err.message : '刪除分類失敗')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Card>
      <h2 className="text-xl font-semibold text-text-main">商品分類</h2>
      <p className="mt-1 text-sm text-text-subtle">全站共用分類，停用後前台刊登就不會再出現。</p>

      <div className="mt-4 space-y-3">
        <input
          value={form.displayName}
          onChange={(event) => setForm((current) => ({ ...current, displayName: event.target.value }))}
          placeholder="分類名稱（例如：家電）"
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
            {editingId !== null ? '更新分類' : '新增分類'}
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
          <p className="text-sm text-text-subtle">載入分類中...</p>
        ) : items.length ? (
          items.map((item) => (
            <div key={item.id} className="rounded-xl border border-border bg-surface p-3">
              <p className="font-medium text-text-main">{item.displayName}</p>
              <p className="mt-1 text-sm text-text-subtle">
                排序 {item.sortOrder}・{item.isActive ? '啟用' : '停用'}・商品 {item.listingCount}
              </p>
              <div className="mt-2 flex flex-wrap gap-2">
                <Button type="button" variant="secondary" onClick={() => handleEdit(item)} disabled={saving}>
                  編輯
                </Button>
                <Button type="button" variant="secondary" onClick={() => void handleToggleEnabled(item)} disabled={saving}>
                  {item.isActive ? '停用' : '啟用'}
                </Button>
                <Button
                  type="button"
                  variant="secondary"
                  onClick={() => void handleDelete(item)}
                  disabled={saving}
                  className="border-[#e9b4b4] bg-[#fbe2e2] text-[#b23a3a] hover:bg-[#f6d3d3]"
                >
                  刪除
                </Button>
              </div>
            </div>
          ))
        ) : (
          <p className="text-sm text-text-subtle">尚無分類</p>
        )}
      </div>
    </Card>
  )
}
