import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useState,
  type PropsWithChildren,
} from 'react'
import { authApi } from '@/features/auth/api/authApi'
import { authStorage } from '@/features/auth/authStorage'
import type { AuthTokens, LoginPayload } from '@/features/auth/types'
import { setupHttpAuth } from '@/shared/api/http'

type AuthContextValue = {
  tokens: AuthTokens | null
  isAuthenticated: boolean
  login: (payload: LoginPayload) => Promise<void>
  logout: () => Promise<void>
  refreshTokens: () => Promise<AuthTokens | null>
  acceptTokens: (tokens: AuthTokens) => void
}

const AuthContext = createContext<AuthContextValue | null>(null)

export const AuthProvider = ({ children }: PropsWithChildren) => {
  const [tokens, setTokens] = useState<AuthTokens | null>(() => authStorage.get())

  const saveTokens = useCallback((nextTokens: AuthTokens | null) => {
    setTokens(nextTokens)
    if (nextTokens) {
      authStorage.set(nextTokens)
      return
    }

    authStorage.clear()
  }, [])

  const refreshTokens = useCallback(async () => {
    if (!tokens?.refreshToken) {
      return null
    }

    try {
      const refreshed = await authApi.refresh(tokens.refreshToken)
      saveTokens(refreshed)
      return refreshed
    } catch {
      saveTokens(null)
      return null
    }
  }, [tokens?.refreshToken, saveTokens])

  const login = useCallback(
    async (payload: LoginPayload) => {
      const nextTokens = await authApi.login(payload)
      saveTokens(nextTokens)
    },
    [saveTokens],
  )

  const logout = useCallback(async () => {
    try {
      if (tokens?.refreshToken) {
        await authApi.revoke(tokens.refreshToken)
      }
    } catch {
      // token may already be invalid, no further action needed
    } finally {
      saveTokens(null)
    }
  }, [saveTokens, tokens?.refreshToken])

  const redirectToLogin = useCallback(() => {
    const { pathname, search, hash } = window.location
    // 避免在登入／註冊頁觸發重複導轉
    if (pathname === '/login' || pathname === '/register') {
      return
    }
    const from = `${pathname}${search}${hash}`
    const encodedFrom = encodeURIComponent(from)
    window.location.replace(`/login?from=${encodedFrom}`)
  }, [])

  // 必須在 render 同步註冊：若放在 useEffect，子元件（如 TopNav）的 effect 會先執行，
  // 首次 /api/v1/account/me 會拿不到 Bearer token，導致問候語卡在「用戶」。
  setupHttpAuth({
    getAccessToken: () => tokens?.accessToken ?? null,
    refreshTokens,
    onUnauthorized: () => {
      saveTokens(null)
      redirectToLogin()
    },
  })

  const value = useMemo<AuthContextValue>(
    () => ({
      tokens,
      isAuthenticated: Boolean(tokens?.accessToken),
      login,
      logout,
      refreshTokens,
      acceptTokens: saveTokens,
    }),
    [login, logout, refreshTokens, saveTokens, tokens],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export const useAuth = () => {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth 必須在 AuthProvider 內使用')
  }

  return context
}
