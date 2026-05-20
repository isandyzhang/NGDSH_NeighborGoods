import { useCallback, useEffect, useState } from 'react'
import { adminApi, type AdminAnnouncement, type UpsertAdminAnnouncementPayload } from '@/features/admin/api/adminApi'
import {
  ANNOUNCEMENT_SCOPE_OPTIONS,
  ANNOUNCEMENT_SEVERITY_OPTIONS,
  getScopeLabel,
  getSeverityLabel,
} from '@/features/admin/constants/announcementOptions'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'

const fieldClassName =
  'w-full rounded-xl border border-border bg-surface px-3 py-2 text-text-main outline-none transition placeholder:text-text-muted focus:border-brand'

const toDatetimeLocalValue = (iso: string | null | undefined) => {
  if (!iso) {
    return ''
  }

  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) {
    return ''
  }

  const pad = (value: number) => String(value).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

const fromDatetimeLocalValue = (value: string) => {
  const trimmed = value.trim()
  if (!trimmed) {
    return null
  }

  const parsed = new Date(trimmed)
  if (Number.isNaN(parsed.getTime())) {
    return null
  }

  return parsed.toISOString()
}

const formatDateTimeRange = (startsAt: string | null, endsAt: string | null) => {
  const format = (iso: string | null) =>
    iso
      ? new Date(iso).toLocaleString('zh-TW', {
          month: '2-digit',
          day: '2-digit',
          hour: '2-digit',
          minute: '2-digit',
        })
      : '—'

  return `${format(startsAt)} ~ ${format(endsAt)}`
}

const emptyForm = () => ({
  message: '',
  severity: 1,
  scope: 0,
  sortOrder: 0,
  isEnabled: true,
  startsAtLocal: '',
  endsAtLocal: '',
  linkUrl: '',
  linkLabel: '',
})

export const AdminAnnouncementsCard = () => {
  const [items, setItems] = useState<AdminAnnouncement[]>([])
  const [form, setForm] = useState(emptyForm)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  const loadAnnouncements = useCallback(async () => {
    const rows = await adminApi.listAnnouncements()
    setItems(rows)
  }, [])

  useEffect(() => {
    let disposed = false
    setLoading(true)
    void loadAnnouncements()
      .catch((err: unknown) => {
        if (!disposed) {
          setMessage(err instanceof ApiClientError ? err.message : '讀取公告失敗')
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
  }, [loadAnnouncements])

  const buildPayload = (): UpsertAdminAnnouncementPayload => ({
    message: form.message.trim(),
    severity: form.severity,
    scope: form.scope,
    sortOrder: form.sortOrder,
    isEnabled: form.isEnabled,
    startsAt: fromDatetimeLocalValue(form.startsAtLocal),
    endsAt: fromDatetimeLocalValue(form.endsAtLocal),
    linkUrl: form.linkUrl.trim() || null,
    linkLabel: form.linkLabel.trim() || null,
  })

  const handleSave = async () => {
    if (saving) {
      return
    }

    setSaving(true)
    setMessage(null)
    try {
      const payload = buildPayload()
      if (editingId) {
        await adminApi.updateAnnouncement(editingId, payload)
        setMessage('公告已更新')
      } else {
        await adminApi.createAnnouncement(payload)
        setMessage('公告已新增')
      }

      setForm(emptyForm())
      setEditingId(null)
      await loadAnnouncements()
    } catch (err) {
      setMessage(err instanceof ApiClientError ? err.message : '儲存公告失敗')
    } finally {
      setSaving(false)
    }
  }

  const handleEdit = (item: AdminAnnouncement) => {
    setEditingId(item.id)
    setForm({
      message: item.message,
      severity: item.severity,
      scope: item.scope,
      sortOrder: item.sortOrder,
      isEnabled: item.isEnabled,
      startsAtLocal: toDatetimeLocalValue(item.startsAt),
      endsAtLocal: toDatetimeLocalValue(item.endsAt),
      linkUrl: item.linkUrl ?? '',
      linkLabel: item.linkLabel ?? '',
    })
    setMessage(null)
  }

  const handleCancelEdit = () => {
    setEditingId(null)
    setForm(emptyForm())
    setMessage(null)
  }

  const handleToggleEnabled = async (item: AdminAnnouncement) => {
    setSaving(true)
    setMessage(null)
    try {
      await adminApi.setAnnouncementEnabled(item.id, !item.isEnabled)
      await loadAnnouncements()
      setMessage(item.isEnabled ? '公告已停用' : '公告已啟用')
    } catch (err) {
      setMessage(err instanceof ApiClientError ? err.message : '更新狀態失敗')
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (item: AdminAnnouncement) => {
    const ok = window.confirm('確定刪除此公告？')
    if (!ok) {
      return
    }

    setSaving(true)
    setMessage(null)
    try {
      await adminApi.deleteAnnouncement(item.id)
      if (editingId === item.id) {
        handleCancelEdit()
      }
      await loadAnnouncements()
      setMessage('公告已刪除')
    } catch (err) {
      setMessage(err instanceof ApiClientError ? err.message : '刪除公告失敗')
    } finally {
      setSaving(false)
    }
  }

  return (
    <Card className="border-[#c5d9f2] bg-[#f4f9ff]">
      <h2 className="text-xl font-semibold text-text-main">跑馬燈公告</h2>
      <p className="mt-1 text-sm text-text-subtle">有啟用中的有效公告時，全站頂部會顯示跑馬燈。</p>

      <div className="mt-4 space-y-3">
        <textarea
          value={form.message}
          onChange={(event) => setForm((current) => ({ ...current, message: event.target.value }))}
          placeholder="公告內容（最多 500 字）"
          rows={3}
          maxLength={500}
          className={fieldClassName}
        />
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <select
            value={form.severity}
            onChange={(event) => setForm((current) => ({ ...current, severity: Number(event.target.value) }))}
            className={fieldClassName}
          >
            {ANNOUNCEMENT_SEVERITY_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
          <select
            value={form.scope}
            onChange={(event) => setForm((current) => ({ ...current, scope: Number(event.target.value) }))}
            className={fieldClassName}
          >
            {ANNOUNCEMENT_SCOPE_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
          <input
            type="number"
            value={form.sortOrder}
            onChange={(event) => setForm((current) => ({ ...current, sortOrder: Number(event.target.value) }))}
            className={fieldClassName}
            placeholder="排序（越小越前）"
          />
          <select
            value={form.isEnabled ? '1' : '0'}
            onChange={(event) => setForm((current) => ({ ...current, isEnabled: event.target.value === '1' }))}
            className={fieldClassName}
          >
            <option value="1">啟用</option>
            <option value="0">停用</option>
          </select>
        </div>
        <div className="grid gap-3 md:grid-cols-2">
          <label className="block text-sm text-text-subtle">
            開始時間（空白=立即）
            <input
              type="datetime-local"
              value={form.startsAtLocal}
              onChange={(event) => setForm((current) => ({ ...current, startsAtLocal: event.target.value }))}
              className={`${fieldClassName} mt-1`}
            />
          </label>
          <label className="block text-sm text-text-subtle">
            結束時間（空白=不自動下架）
            <input
              type="datetime-local"
              value={form.endsAtLocal}
              onChange={(event) => setForm((current) => ({ ...current, endsAtLocal: event.target.value }))}
              className={`${fieldClassName} mt-1`}
            />
          </label>
        </div>
        <div className="grid gap-3 md:grid-cols-2">
          <input
            value={form.linkUrl}
            onChange={(event) => setForm((current) => ({ ...current, linkUrl: event.target.value }))}
            placeholder="詳情連結（可選）"
            className={fieldClassName}
          />
          <input
            value={form.linkLabel}
            onChange={(event) => setForm((current) => ({ ...current, linkLabel: event.target.value }))}
            placeholder="連結文字（可選）"
            className={fieldClassName}
          />
        </div>
        <div className="flex flex-wrap gap-2">
          <Button type="button" onClick={() => void handleSave()} disabled={saving || !form.message.trim()}>
            {editingId ? '更新公告' : '新增公告'}
          </Button>
          {editingId ? (
            <Button type="button" variant="secondary" onClick={handleCancelEdit} disabled={saving}>
              取消編輯
            </Button>
          ) : null}
        </div>
      </div>

      {message ? <p className="mt-2 text-sm text-text-subtle">{message}</p> : null}

      <div className="mt-4 space-y-2">
        {loading ? (
          <p className="text-sm text-text-subtle">載入公告中...</p>
        ) : items.length ? (
          items.map((item) => (
            <div key={item.id} className="rounded-xl border border-border bg-surface p-3">
              <p className="font-medium text-text-main">{item.message}</p>
              <p className="mt-1 text-sm text-text-subtle">
                {getSeverityLabel(item.severity)}・{getScopeLabel(item.scope)}・排序 {item.sortOrder}・
                {item.isEnabled ? '啟用' : '停用'}
              </p>
              <p className="text-sm text-text-subtle">生效：{formatDateTimeRange(item.startsAt, item.endsAt)}</p>
              <div className="mt-2 flex flex-wrap gap-2">
                <Button type="button" variant="secondary" onClick={() => handleEdit(item)} disabled={saving}>
                  編輯
                </Button>
                <Button type="button" variant="secondary" onClick={() => void handleToggleEnabled(item)} disabled={saving}>
                  {item.isEnabled ? '停用' : '啟用'}
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
          <p className="text-sm text-text-subtle">尚無公告</p>
        )}
      </div>
    </Card>
  )
}
