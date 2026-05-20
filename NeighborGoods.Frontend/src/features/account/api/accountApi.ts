import { http } from '@/shared/api/http'
import { unwrapApiResponse, type ApiResponse } from '@/shared/types/api'

export type AccountMe = {
  userId: string
  userName: string
  displayName: string
  role: number
  email: string | null
  emailConfirmed: boolean
  emailNotificationEnabled: boolean
  lineContactId: string | null
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

type UpdateProfilePayload = {
  displayName?: string
  lineContactId?: string
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
  role: number
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
  liffUrl: string
  bindingToken: string
  botLink: string
}

type CompleteLineBindingPayload = {
  bindingToken: string
  idToken: string
}

export const accountApi = {
  async me(): Promise<AccountMe> {
    const response = await http.get<ApiResponse<AccountMe>>('/api/v1/account/me')
    return unwrapApiResponse(response.data)
  },

  async updateProfile(payload: UpdateProfilePayload): Promise<{ updated: boolean }> {
    const response = await http.patch<ApiResponse<{ updated: boolean }>>('/api/v1/account/me', payload)
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

  async completeLineBinding(payload: CompleteLineBindingPayload): Promise<{ bound: boolean }> {
    const response = await http.post<ApiResponse<{ bound: boolean }>>('/api/v1/account/line/bind/liff-complete', payload)
    return unwrapApiResponse(response.data)
  },

  async unbindLineBinding(): Promise<{ unbound: boolean }> {
    const response = await http.post<ApiResponse<{ unbound: boolean }>>('/api/v1/account/line/bind/unbind')
    return unwrapApiResponse(response.data)
  },

  async sendListingEmailCode(email: string): Promise<{ sent: boolean }> {
    const response = await http.post<ApiResponse<{ sent: boolean }>>('/api/v1/account/email/send-code', { email })
    return unwrapApiResponse(response.data)
  },

  async verifyListingEmail(email: string, code: string): Promise<{ verified: boolean }> {
    const response = await http.post<ApiResponse<{ verified: boolean }>>('/api/v1/account/email/verify', {
      email,
      code,
    })
    return unwrapApiResponse(response.data)
  },

  async disableNotifications(): Promise<{ emailNotificationEnabled: boolean }> {
    const response = await http.post<ApiResponse<{ emailNotificationEnabled: boolean }>>(
      '/api/v1/account/notifications/disable',
    )
    return unwrapApiResponse(response.data)
  },

  async enableEmailNotifications(): Promise<{ emailNotificationEnabled: boolean }> {
    const response = await http.post<ApiResponse<{ emailNotificationEnabled: boolean }>>(
      '/api/v1/account/notifications/email/enable',
    )
    return unwrapApiResponse(response.data)
  },

  async disableEmailNotifications(): Promise<{ emailNotificationEnabled: boolean }> {
    const response = await http.post<ApiResponse<{ emailNotificationEnabled: boolean }>>(
      '/api/v1/account/notifications/email/disable',
    )
    return unwrapApiResponse(response.data)
  },

  async disableLineNotifications(): Promise<{ lineNotificationsDisabled: boolean }> {
    const response = await http.post<ApiResponse<{ lineNotificationsDisabled: boolean }>>(
      '/api/v1/account/notifications/line/disable',
    )
    return unwrapApiResponse(response.data)
  },
}
