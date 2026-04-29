import { useMemo, useState } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { accountApi } from '@/features/account/api/accountApi'
import { useAuth } from '@/features/auth/components/AuthProvider'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { Input } from '@/shared/ui/Input'

export const RegisterPage = () => {
  const navigate = useNavigate()
  const { isAuthenticated, acceptTokens } = useAuth()
  const [userName, setUserName] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [emailVerificationCode, setEmailVerificationCode] = useState('')
  const [sendingCode, setSendingCode] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [successText, setSuccessText] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const canSubmit = useMemo(
    () =>
      userName.trim().length >= 3 &&
      displayName.trim().length >= 2 &&
      email.trim().length > 0 &&
      password.length >= 8 &&
      emailVerificationCode.trim().length >= 4,
    [displayName, email, emailVerificationCode, password.length, userName],
  )

  if (isAuthenticated) {
    return <Navigate to="/listings" replace />
  }

  const handleSendCode = async () => {
    if (sendingCode || !email.trim()) {
      return
    }

    setSendingCode(true)
    setError(null)
    setSuccessText(null)
    try {
      await accountApi.sendRegisterCode(email.trim())
      setSuccessText('驗證碼已寄出，請到信箱確認。')
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '寄送驗證碼失敗')
    } finally {
      setSendingCode(false)
    }
  }

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!canSubmit) {
      setError('請完成所有欄位，並確認密碼至少 8 碼')
      return
    }

    setSubmitting(true)
    setError(null)
    setSuccessText(null)
    try {
      const tokens = await accountApi.register({
        userName: userName.trim(),
        displayName: displayName.trim(),
        email: email.trim(),
        password,
        emailVerificationCode: emailVerificationCode.trim(),
      })
      acceptTokens(tokens)
      navigate('/listings', { replace: true })
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '註冊失敗')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <main className="mx-auto w-full max-w-4xl px-4 py-6 md:py-8">
      <div className="mx-auto flex w-full max-w-2xl flex-col gap-4 md:gap-5">
        <section className="space-y-3 px-2 py-1 text-center">
          <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
          <h1 className="text-4xl font-semibold leading-tight text-text-main sm:text-5xl md:text-6xl">
            建立<span className="marker-wipe">帳號</span>
          </h1>
          <p className="text-base text-text-subtle">完成註冊後可立即登入，開始收藏、刊登與私訊。</p>
        </section>

        <Card>
          <form className="space-y-4" onSubmit={handleSubmit}>
            <Input label="帳號" value={userName} onChange={(event) => setUserName(event.target.value)} placeholder="至少 3 個字元" required />
            <Input
              label="顯示名稱"
              value={displayName}
              onChange={(event) => setDisplayName(event.target.value)}
              placeholder="顯示在網頁上的名稱"
              required
            />
            <div className="grid gap-2 sm:grid-cols-[1fr_auto]">
              <Input
                label="Email"
                type="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                placeholder="you@example.com"
                required
              />
              <Button
                type="button"
                variant="secondary"
                className="self-end px-5 py-3 text-base font-semibold"
                disabled={sendingCode || !email.trim()}
                onClick={() => void handleSendCode()}
              >
                {sendingCode ? '寄送中...' : '寄驗證碼'}
              </Button>
            </div>
            <Input
              label="Email 驗證碼"
              value={emailVerificationCode}
              onChange={(event) => setEmailVerificationCode(event.target.value)}
              placeholder="輸入信件中的驗證碼"
              required
            />
            <Input
              label="密碼"
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              placeholder="至少 8 碼"
              required
            />
            {error ? <p className="text-sm text-danger">{error}</p> : null}
            {successText ? <p className="text-sm text-[#2F7D4E]">{successText}</p> : null}
            <Button type="submit" fullWidth className="min-h-[3rem] text-base font-semibold" disabled={submitting || !canSubmit}>
              {submitting ? '註冊中...' : '完成註冊'}
            </Button>
          </form>

          <p className="mt-4 text-center text-sm text-text-subtle">
            已有帳號？{' '}
            <Link to="/login" className="font-medium text-text-main underline-offset-2 hover:underline">
              回到登入
            </Link>
          </p>
        </Card>
      </div>
    </main>
  )
}
