import { http } from '@/shared/api/http'
import { unwrapApiResponse, type ApiResponse } from '@/shared/types/api'

export type AccountMe = {
  userId: string
  userName: string
  displayName: string
  email: string | null
  emailConfirmed: boolean
  lineUserId: string | null
  createdAt: string
  statistics: {
    totalListings: number
    activeListings: number
    completedListings: number
    topPinCredits: number
  }
}

export const accountApi = {
  async me(): Promise<AccountMe> {
    const response = await http.get<ApiResponse<AccountMe>>('/api/v1/account/me')
    return unwrapApiResponse(response.data)
  },
}
