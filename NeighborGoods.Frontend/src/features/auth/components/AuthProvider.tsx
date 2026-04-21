import {
  createContext,
  useCallback,
  useContext,
  useEffect,
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

  useEffect(() => {
    setupHttpAuth({
      getAccessToken: () => tokens?.accessToken ?? null,
      refreshTokens,
      onUnauthorized: () => saveTokens(null),
    })
  }, [refreshTokens, saveTokens, tokens?.accessToken])

  const value = useMemo<AuthContextValue>(
    () => ({
      tokens,
      isAuthenticated: Boolean(tokens?.accessToken),
      login,
      logout,
      refreshTokens,
    }),
    [login, logout, refreshTokens, tokens],
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
