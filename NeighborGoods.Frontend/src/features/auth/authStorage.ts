import type { AuthTokens } from './types'

const AUTH_STORAGE_KEY = 'neighborGoods.authTokens'

export const authStorage = {
  get(): AuthTokens | null {
    const raw = localStorage.getItem(AUTH_STORAGE_KEY)
    if (!raw) {
      return null
    }

    try {
      return JSON.parse(raw) as AuthTokens
    } catch {
      localStorage.removeItem(AUTH_STORAGE_KEY)
      return null
    }
  },
  set(tokens: AuthTokens): void {
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(tokens))
  },
  clear(): void {
    localStorage.removeItem(AUTH_STORAGE_KEY)
  },
}
