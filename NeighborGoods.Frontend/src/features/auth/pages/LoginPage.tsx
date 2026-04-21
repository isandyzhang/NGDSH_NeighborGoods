import { useState } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '@/features/auth/components/AuthProvider'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { Input } from '@/shared/ui/Input'

type LocationState = {
  from?: string
}

export const LoginPage = () => {
  const navigate = useNavigate()
  const location = useLocation()
  const { login, isAuthenticated } = useAuth()
  const [userNameOrEmail, setUserNameOrEmail] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  if (isAuthenticated) {
    const destination = (location.state as LocationState | null)?.from ?? '/listings'
    return <Navigate to={destination} replace />
  }

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError(null)
    setLoading(true)

    try {
      await login({ userNameOrEmail, password })
      const destination = (location.state as LocationState | null)?.from ?? '/listings'
      navigate(destination, { replace: true })
    } catch (err) {
      if (err instanceof ApiClientError) {
        setError(err.message)
      } else {
        setError('登入失敗，請稍後再試')
      }
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="mx-auto flex min-h-[calc(100vh-4rem)] w-full max-w-6xl items-center justify-center px-4 py-6 md:py-10">
      <div className="grid w-full max-w-4xl gap-6 md:grid-cols-2 md:gap-8">
        <section className="space-y-4">
          <p className="text-sm uppercase tracking-[0.18em] text-text-subtle">NeighborGoods</p>
          <h1 className="text-3xl font-semibold leading-tight text-text-main sm:text-4xl md:text-5xl">
            Dynamic Community Market.
          </h1>
          <p className="max-w-md text-text-subtle">
            使用同一組帳號登入，快速瀏覽社區物品、管理刊登並即時回覆訊息。
          </p>
        </section>
        <Card className="self-center">
          <h2 className="mb-6 text-2xl font-semibold text-text-main">登入</h2>
          <form className="space-y-4" onSubmit={handleSubmit}>
            <Input
              label="帳號或 Email"
              value={userNameOrEmail}
              onChange={(event) => setUserNameOrEmail(event.target.value)}
              placeholder="you@example.com"
              required
            />
            <Input
              label="密碼"
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              placeholder="••••••••"
              required
            />
            {error ? <p className="text-sm text-danger">{error}</p> : null}
            <Button type="submit" fullWidth disabled={loading}>
              {loading ? '登入中...' : '登入'}
            </Button>
          </form>
        </Card>
      </div>
    </main>
  )
}
