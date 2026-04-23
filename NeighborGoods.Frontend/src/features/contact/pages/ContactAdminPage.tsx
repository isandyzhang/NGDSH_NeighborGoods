import { useMemo, useState } from 'react'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { Input } from '@/shared/ui/Input'

const ADMIN_EMAIL = 'support@neighborgoods.local'

export const ContactAdminPage = () => {
  const [name, setName] = useState('')
  const [email, setEmail] = useState('')
  const [subject, setSubject] = useState('')
  const [message, setMessage] = useState('')
  const [successText, setSuccessText] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const canSubmit = useMemo(
    () => name.trim().length > 0 && email.trim().length > 0 && subject.trim().length > 0 && message.trim().length >= 10,
    [email, message, name, subject],
  )

  const handleSubmit = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError(null)
    setSuccessText(null)

    if (!canSubmit) {
      setError('請完成所有欄位，且訊息至少 10 個字')
      return
    }

    const body = [
      `聯絡人：${name}`,
      `聯絡 Email：${email}`,
      '',
      message,
    ].join('\n')

    const mailtoUrl = `mailto:${ADMIN_EMAIL}?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`
    window.location.href = mailtoUrl
    setSuccessText('已開啟郵件程式，請確認內容後送出。')
  }

  return (
    <main className="mx-auto w-full max-w-4xl px-4 py-6 md:py-8">
      <section className="mb-8 space-y-3 text-center">
        <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
        <h1 className="text-4xl font-semibold leading-tight text-text-main sm:text-5xl md:text-6xl">
          聯絡<span className="marker-wipe">管理員</span>
        </h1>
        <p className="text-xl text-text-subtle">問題回報、功能建議或帳號協助都可以在這裡送出。</p>
      </section>

      <Card>
        <form className="space-y-5" onSubmit={handleSubmit}>
          <Input
            label="姓名"
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="請輸入你的姓名"
            className="py-3 text-xl"
            labelClassName="text-[1.45rem] font-bold text-text-main"
            required
          />
          <Input
            label="Email"
            type="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            placeholder="you@example.com"
            className="py-3 text-xl"
            labelClassName="text-[1.45rem] font-bold text-text-main"
            required
          />
          <Input
            label="主旨"
            value={subject}
            onChange={(event) => setSubject(event.target.value)}
            placeholder="例如：無法登入、功能建議"
            className="py-3 text-xl"
            labelClassName="text-[1.45rem] font-bold text-text-main"
            required
          />
          <label className="flex flex-col gap-2 text-lg text-text-subtle">
            <span className="text-[1.45rem] font-bold leading-tight text-text-main">訊息內容</span>
            <textarea
              value={message}
              onChange={(event) => setMessage(event.target.value)}
              className="min-h-36 w-full rounded-xl border border-border bg-surface px-3 py-3 text-xl text-text-main outline-none transition focus:border-brand"
              placeholder="請描述你的問題與重現步驟..."
              maxLength={2000}
            />
          </label>
          <p className="text-base text-text-muted">目前會透過郵件程式送出至：{ADMIN_EMAIL}</p>
          {error ? <p className="text-base text-danger">{error}</p> : null}
          {successText ? <p className="text-base text-[#2F7D4E]">{successText}</p> : null}
          <Button type="submit" fullWidth className="min-h-[3.4rem] text-xl font-semibold">
            送出
          </Button>
        </form>
      </Card>
    </main>
  )
}
