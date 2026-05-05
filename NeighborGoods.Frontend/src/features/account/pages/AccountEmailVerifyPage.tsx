import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { accountApi } from '@/features/account/api/accountApi'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { ErrorState } from '@/shared/ui/state/ErrorState'
import { Input } from '@/shared/ui/Input'
import { PageSkeleton } from '@/shared/ui/state/PageSkeleton'

export const AccountEmailVerifyPage = () => {
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [code, setCode] = useState('')
  const [loadingProfile, setLoadingProfile] = useState(true)
  const [sendingCode, setSendingCode] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [successText, setSuccessText] = useState<string | null>(null)

  useEffect(() => {
    let disposed = false
    void accountApi
      .me()
      .then((me) => {
        if (!disposed && me.email) {
          setEmail(me.email)
        }
      })
      .catch(() => undefined)
      .finally(() => {
        if (!disposed) {
          setLoadingProfile(false)
        }
      })
    return () => {
      disposed = true
    }
  }, [])

  const handleSendCode = useCallback(async () => {
    if (sendingCode || !email.trim()) {
      return
    }
    setSendingCode(true)
    setError(null)
    setSuccessText(null)
    try {
      await accountApi.sendListingEmailCode(email.trim())
      setSuccessText('驗證碼已寄出，請到信箱查收。')
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '寄送驗證碼失敗')
    } finally {
      setSendingCode(false)
    }
  }, [email, sendingCode])

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!email.trim() || !code.trim()) {
      setError('請填寫 Email 與驗證碼')
      return
    }
    setSubmitting(true)
    setError(null)
    setSuccessText(null)
    try {
      await accountApi.verifyListingEmail(email.trim(), code.trim())
      navigate('/account', { replace: true })
    } catch (err) {
      setError(err instanceof ApiClientError ? err.message : '驗證失敗')
    } finally {
      setSubmitting(false)
    }
  }

  if (loadingProfile) {
    return <PageSkeleton className="mx-auto h-64 max-w-2xl" />
  }

  return (
    <main className="mx-auto w-full max-w-4xl px-4 py-6 md:py-8">
      <div className="mx-auto flex w-full max-w-2xl flex-col gap-4">
        <div className="text-center">
          <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
          <h1 className="mt-2 text-3xl font-semibold text-text-main">驗證 Email</h1>
          <p className="mt-1 text-sm text-text-subtle">完成驗證後可收到重要通知信。</p>
        </div>

        {error ? <ErrorState description={error} /> : null}
        {successText ? <p className="text-sm text-[#2F7D4E]">{successText}</p> : null}

        <Card>
          <form className="space-y-4" onSubmit={handleSubmit}>
            <Input
              type="email"
              label="Email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="用於收信的 Email"
              required
            />
            <div className="flex flex-wrap gap-2">
              <Button
                type="button"
                variant="secondary"
                disabled={sendingCode || !email.trim()}
                onClick={() => void handleSendCode()}
              >
                {sendingCode ? '寄送中...' : '寄送驗證碼'}
              </Button>
            </div>
            <Input
              label="驗證碼"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              placeholder="輸入信件中的驗證碼"
              inputMode="numeric"
              autoComplete="one-time-code"
            />
            <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
              <Button type="submit" className="min-w-[8rem]" disabled={submitting}>
                {submitting ? '驗證中...' : '完成驗證'}
              </Button>
              <Link
                to="/account"
                className="text-center text-sm font-semibold text-text-subtle underline decoration-dotted underline-offset-2 hover:text-text-main"
              >
                返回我的帳號
              </Link>
            </div>
          </form>
        </Card>
      </div>
    </main>
  )
}
