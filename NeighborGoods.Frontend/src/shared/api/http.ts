import axios, { AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { env } from '@/shared/config/env'
import { ApiClientError, type ApiErrorResponse } from '@/shared/types/api'
import type { AuthTokens } from '@/features/auth/types'

type RefreshHandler = () => Promise<AuthTokens | null>
type GetTokenHandler = () => string | null
type LogoutHandler = () => void

type RetriableConfig = InternalAxiosRequestConfig & {
  _retry?: boolean
}

let getTokenHandler: GetTokenHandler = () => null
let refreshHandler: RefreshHandler = async () => null
let logoutHandler: LogoutHandler = () => {}
let refreshPromise: Promise<AuthTokens | null> | null = null

export const setupHttpAuth = (handlers: {
  getAccessToken: GetTokenHandler
  refreshTokens: RefreshHandler
  onUnauthorized: LogoutHandler
}) => {
  getTokenHandler = handlers.getAccessToken
  refreshHandler = handlers.refreshTokens
  logoutHandler = handlers.onUnauthorized
}

export const http = axios.create({
  baseURL: env.apiBaseUrl,
  timeout: 15000,
})

const normalizeNetworkErrorMessage = (error: AxiosError<ApiErrorResponse>) => {
  if (error.code === 'ECONNABORTED') {
    return '連線逾時（15 秒），請稍後再試一次'
  }

  if (error.message?.toLowerCase().includes('network error')) {
    return '網路連線失敗，請確認後端服務是否啟動'
  }

  return error.message || '發生未預期錯誤'
}

http.interceptors.request.use((config) => {
  const token = getTokenHandler()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

http.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ApiErrorResponse>) => {
    const config = error.config as RetriableConfig | undefined
    const status = error.response?.status
    const requestUrl = config?.url ?? ''
    const isRefreshRequest = requestUrl.includes('/api/v1/auth/refresh')

    if (status === 401 && config && !config._retry && !isRefreshRequest) {
      config._retry = true
      if (!refreshPromise) {
        refreshPromise = refreshHandler().finally(() => {
          refreshPromise = null
        })
      }

      const tokens = await refreshPromise
      if (tokens) {
        config.headers.Authorization = `Bearer ${tokens.accessToken}`
        return http(config)
      }

      logoutHandler()
    }

    if (error.response?.data && !error.response.data.success) {
      throw new ApiClientError(
        error.response.data.error.message,
        error.response.data.error.code,
        error.response.status,
      )
    }

    throw new ApiClientError(normalizeNetworkErrorMessage(error), 'NETWORK_ERROR', status)
  },
)
