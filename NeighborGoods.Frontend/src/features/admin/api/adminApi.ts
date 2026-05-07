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

export type AdminListingManagement = {
  items: Array<{
    id: string
    title: string
    sellerDisplayName: string
    price: number
    isFree: boolean
    status: number
    isPinned: boolean
    createdAt: string
  }>
  pagination: {
    page: number
    pageSize: number
    totalCount: number
    totalPages: number
  }
}

export const adminApi = {
  async getDashboard(): Promise<AdminDashboard> {
    const response = await http.get<ApiResponse<AdminDashboard>>('/api/v1/admin/dashboard')
    return unwrapApiResponse(response.data)
  },

  async forceUpdateListingStatus(listingId: string, status: number): Promise<{ id: string; status: number; forced: boolean }> {
    const response = await http.patch<ApiResponse<{ id: string; status: number; forced: boolean }>>(
      `/api/v1/admin/listings/${listingId}/force-status`,
      { status },
    )
    return unwrapApiResponse(response.data)
  },

  async hardDeleteListing(listingId: string): Promise<{ id: string; deleted: boolean; hardDeleted: boolean }> {
    const response = await http.delete<ApiResponse<{ id: string; deleted: boolean; hardDeleted: boolean }>>(
      `/api/v1/admin/listings/${listingId}/hard-delete`,
    )
    return unwrapApiResponse(response.data)
  },

  async listListings(params: { q?: string; status?: number; page?: number; pageSize?: number }): Promise<AdminListingManagement> {
    const response = await http.get<ApiResponse<AdminListingManagement>>('/api/v1/admin/listings', { params })
    return unwrapApiResponse(response.data)
  },

  async batchForceUpdateListingStatus(listingIds: string[], status: number): Promise<{ updatedCount: number; status: number }> {
    const response = await http.patch<ApiResponse<{ updatedCount: number; status: number }>>('/api/v1/admin/listings/batch-status', {
      listingIds,
      status,
    })
    return unwrapApiResponse(response.data)
  },
}
