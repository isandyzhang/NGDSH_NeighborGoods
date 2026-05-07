import { http } from '@/shared/api/http'
import { unwrapApiResponse, type ApiResponse } from '@/shared/types/api'

export type AdminDashboard = {
  kpi: {
    totalListings: number
    activeListings: number
    completedListings: number
    pendingTopSubmissions: number
    unreadAdminMessages: number
  }
  latestListings: Array<{
    id: string
    title: string
    sellerDisplayName: string
    price: number
    isFree: boolean
    status: number
    isPinned: boolean
    createdAt: string
  }>
  latestMessages: Array<{
    id: string
    senderDisplayName: string
    content: string
    isRead: boolean
    createdAt: string
  }>
  latestTopSubmissions: Array<{
    id: number
    userDisplayName: string
    listingId: string | null
    feedbackTitle: string
    status: number
    createdAt: string
  }>
}

export const adminApi = {
  async getDashboard(): Promise<AdminDashboard> {
    const response = await http.get<ApiResponse<AdminDashboard>>('/api/v1/admin/dashboard')
    return unwrapApiResponse(response.data)
  },
}
