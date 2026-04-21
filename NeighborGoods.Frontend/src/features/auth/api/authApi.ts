import { http } from '@/shared/api/http'
import { unwrapApiResponse, type ApiResponse } from '@/shared/types/api'
import type { AuthTokens, LoginPayload } from '../types'

type RevokeResponse = { revoked: boolean }

export const authApi = {
  async login(payload: LoginPayload): Promise<AuthTokens> {
    const response = await http.post<ApiResponse<AuthTokens>>('/api/v1/auth/login', payload)
    return unwrapApiResponse(response.data)
  },

  async refresh(refreshToken: string): Promise<AuthTokens> {
    const response = await http.post<ApiResponse<AuthTokens>>('/api/v1/auth/refresh', { refreshToken })
    return unwrapApiResponse(response.data)
  },

  async revoke(refreshToken: string): Promise<RevokeResponse> {
    const response = await http.post<ApiResponse<RevokeResponse>>('/api/v1/auth/revoke', { refreshToken })
    return unwrapApiResponse(response.data)
  },
}
