import { http } from '@/shared/api/http'
import { unwrapApiResponse, type ApiResponse } from '@/shared/types/api'

export type AccountMe = {
  userId: string
  userName: string
  displayName: string
  email: string | null
  emailConfirmed: boolean
  lineUserId: string | null
  lineNotifyBound: boolean
  createdAt: string
  statistics: {
    totalListings: number
    activeListings: number
    completedListings: number
    topPinCredits: number
  }
}

type RegisterPayload = {
  userName: string
  displayName: string
  email: string
  password: string
  emailVerificationCode: string
}

type AuthTokens = {
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
  userId: string
}

export type LinePreferences = {
  marketingPushEnabled: boolean
  preferenceNewListings: boolean
  preferencePriceDrop: boolean
  preferenceMessageDigest: boolean
  lastPreferencePushSentAt: string | null
}

export type LineQuotaStatus = {
  isEstimated: boolean
  planType: string
  monthlyQuota: number | null
  usedCount: number
  remainingCount: number | null
  usagePercent: number | null
  note: string
}

export type StartLineBindingResponse = {
  pendingBindingId: string
  botLink: string
  qrCodeUrl: string
}

export type LineBindingStatusResponse = {
  status: 'waiting' | 'ready' | 'completed' | 'not_found'
  message: string
  lineUserId?: string | null
}

export const accountApi = {
  async me(): Promise<AccountMe> {
    const response = await http.get<ApiResponse<AccountMe>>('/api/v1/account/me')
    return unwrapApiResponse(response.data)
  },

  async sendRegisterCode(email: string): Promise<{ sent: boolean }> {
    const response = await http.post<ApiResponse<{ sent: boolean }>>('/api/v1/account/register/send-code', { email })
    return unwrapApiResponse(response.data)
  },

  async register(payload: RegisterPayload): Promise<AuthTokens> {
    const response = await http.post<ApiResponse<AuthTokens>>('/api/v1/account/register', payload)
    return unwrapApiResponse(response.data)
  },

  async getLinePreferences(): Promise<LinePreferences> {
    const response = await http.get<ApiResponse<LinePreferences>>('/api/v1/account/line/preferences')
    return unwrapApiResponse(response.data)
  },

  async updateLinePreferences(payload: Omit<LinePreferences, 'lastPreferencePushSentAt'>): Promise<LinePreferences> {
    const response = await http.patch<ApiResponse<LinePreferences>>('/api/v1/account/line/preferences', payload)
    return unwrapApiResponse(response.data)
  },

  async getLineQuota(): Promise<LineQuotaStatus> {
    const response = await http.get<ApiResponse<LineQuotaStatus>>('/api/v1/account/line/quota')
    return unwrapApiResponse(response.data)
  },

  async startLineBinding(): Promise<StartLineBindingResponse> {
    const response = await http.post<ApiResponse<StartLineBindingResponse>>('/api/v1/account/line/bind/start')
    return unwrapApiResponse(response.data)
  },

  async getLineBindingStatus(pendingBindingId: string): Promise<LineBindingStatusResponse> {
    const response = await http.get<ApiResponse<LineBindingStatusResponse>>('/api/v1/account/line/bind/status', {
      params: { pendingBindingId },
    })
    return unwrapApiResponse(response.data)
  },

  async confirmLineBinding(pendingBindingId: string): Promise<{ bound: boolean }> {
    const response = await http.post<ApiResponse<{ bound: boolean }>>('/api/v1/account/line/bind/confirm', {
      pendingBindingId,
    })
    return unwrapApiResponse(response.data)
  },
}
