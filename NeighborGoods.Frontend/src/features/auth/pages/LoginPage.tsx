import { useState } from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { authApi } from '@/features/auth/api/authApi'
import { useAuth } from '@/features/auth/components/AuthProvider'
import { ApiClientError } from '@/shared/types/api'
import { Button } from '@/shared/ui/Button'
import { Card } from '@/shared/ui/Card'
import { Input } from '@/shared/ui/Input'

type LocationState = {
  from?: string
}

const LINE_RETURN_TO_KEY = 'line_login_return_to'
const LINE_LOGIN_ICON = new URL('../../../png/line_icon.png', import.meta.url).href

export const LoginPage = () => {
  const navigate = useNavigate()
  const location = useLocation()
  const { login, isAuthenticated } = useAuth()
  const [userNameOrEmail, setUserNameOrEmail] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [showEmailForm, setShowEmailForm] = useState(false)

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

  const handleLineLogin = () => {
    const destination = (location.state as LocationState | null)?.from ?? '/listings'
    sessionStorage.setItem(LINE_RETURN_TO_KEY, destination)
    window.location.assign(authApi.buildLineLoginUrl())
  }

  const handleShowEmailForm = () => {
    setShowEmailForm((current) => !current)
    setError(null)
  }

  return (
    <main className="mx-auto flex min-h-[calc(100vh-4rem)] w-full max-w-6xl items-start justify-center px-4 pb-8 pt-6 md:items-center md:py-10">
      <div className="grid w-full max-w-5xl gap-8 md:grid-cols-[1.1fr_0.9fr] md:items-center md:gap-10">
        <motion.section
          className="space-y-4 text-center md:text-left"
          initial={{ opacity: 0, y: 36 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.65, ease: [0.22, 1, 0.36, 1] }}
        >
          <motion.p
            className="text-sm uppercase tracking-[0.18em] text-text-subtle"
            initial={{ opacity: 0, y: 18 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.45, delay: 0.08, ease: [0.22, 1, 0.36, 1] }}
          >
            NeighborGoods
          </motion.p>
          <motion.h1
            className="text-5xl font-semibold leading-tight text-text-main sm:text-6xl md:text-7xl"
            initial={{ opacity: 0, y: 24 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.58, delay: 0.16, ease: [0.22, 1, 0.36, 1] }}
          >
            <span className="block">
              社宅<span className="marker-wipe">專屬</span>
            </span>
            <span className="block">二手交易平台</span>
          </motion.h1>
          <motion.p
            className="mx-auto max-w-md text-lg text-text-subtle md:mx-0"
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.55, delay: 0.28, ease: [0.22, 1, 0.36, 1] }}
          >
            使用同一組帳號登入，快速瀏覽社區物品、管理刊登並即時回覆訊息。
          </motion.p>
        </motion.section>
        <motion.div
          className="flex justify-center md:justify-end"
          initial={{ opacity: 0, x: 42, y: 10, scale: 0.97 }}
          animate={{ opacity: 1, x: 0, y: 0, scale: 1 }}
          transition={{ duration: 0.62, delay: 0.52, ease: [0.22, 1, 0.36, 1] }}
        >
          <Card className="w-full max-w-[24rem] md:max-w-[25rem]">
            <h2 className="mb-6 text-center text-4xl font-semibold text-text-main">登入</h2>
            <div className="space-y-3">
              <Button
                type="button"
                variant="secondary"
                onClick={handleShowEmailForm}
                fullWidth
                className="h-12 text-base font-semibold"
              >
                {showEmailForm ? '收起 Email 登入' : '使用 Email 登入'}
              </Button>
              <AnimatePresence initial={false}>
                {showEmailForm ? (
                  <motion.form
                    key="email-form"
                    className="space-y-4 overflow-hidden"
                    onSubmit={handleSubmit}
                    initial={{ opacity: 0, height: 0, y: -8 }}
                    animate={{ opacity: 1, height: 'auto', y: 0 }}
                    exit={{ opacity: 0, height: 0, y: -8 }}
                    transition={{ duration: 0.24, ease: [0.22, 1, 0.36, 1] }}
                  >
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
                    <Button type="submit" fullWidth disabled={loading} className="h-12 text-lg font-semibold">
                      {loading ? '登入中...' : '登入'}
                    </Button>
                  </motion.form>
                ) : null}
              </AnimatePresence>
              <Button
                type="button"
                onClick={handleLineLogin}
                fullWidth
                className="flex h-12 items-center justify-center gap-3 !bg-[#06C755] text-base font-semibold !text-white hover:!bg-[#05b64d]"
              >
                <span className="inline-flex h-6 w-6 items-center justify-center rounded bg-white/20">
                  <img src={LINE_LOGIN_ICON} alt="" className="h-4 w-4 object-contain" aria-hidden="true" />
                </span>
                <span className="tracking-[0.02em]">使用 LINE 登入</span>
              </Button>
              <p className="text-center text-sm text-text-subtle">
                還沒有帳號？{' '}
                <Link to="/register" className="font-medium text-text-main underline-offset-2 hover:underline">
                  前往註冊
                </Link>
              </p>
            </div>
          </Card>
        </motion.div>
      </div>
    </main>
  )
}
