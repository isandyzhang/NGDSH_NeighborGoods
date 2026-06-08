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

export type AdminAnnouncement = {
  id: string
  message: string
  severity: number
  scope: number
  sortOrder: number
  isEnabled: boolean
  startsAt: string | null
  endsAt: string | null
  linkUrl: string | null
  linkLabel: string | null
  createdAt: string
  createdByUserId: string | null
  updatedAt: string | null
  updatedByUserId: string | null
}

export type UpsertAdminAnnouncementPayload = {
  message: string
  severity: number
  scope: number
  sortOrder: number
  isEnabled: boolean
  startsAt: string | null
  endsAt: string | null
  linkUrl: string | null
  linkLabel: string | null
}

export type AdminConversationListItem = {
  conversationId: string
  listingId: string
  listingTitle: string
  participant1Id: string
  participant1DisplayName: string
  participant2Id: string
  participant2DisplayName: string
  lastMessagePreview: string | null
  lastMessageAt: string | null
  updatedAt: string
  messageCount: number
}

export type AdminConversationList = {
  items: AdminConversationListItem[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export type AdminConversationMessage = {
  id: string
  conversationId: string
  senderId: string
  senderDisplayName: string
  content: string
  createdAt: string
}

export type AdminConversationMessages = {
  items: AdminConversationMessage[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
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

  async listAnnouncements(): Promise<AdminAnnouncement[]> {
    const response = await http.get<ApiResponse<{ items: AdminAnnouncement[] }>>('/api/v1/admin/announcements')
    return unwrapApiResponse(response.data).items
  },

  async createAnnouncement(payload: UpsertAdminAnnouncementPayload): Promise<AdminAnnouncement> {
    const response = await http.post<ApiResponse<AdminAnnouncement>>('/api/v1/admin/announcements', payload)
    return unwrapApiResponse(response.data)
  },

  async updateAnnouncement(id: string, payload: UpsertAdminAnnouncementPayload): Promise<AdminAnnouncement> {
    const response = await http.patch<ApiResponse<AdminAnnouncement>>(`/api/v1/admin/announcements/${id}`, payload)
    return unwrapApiResponse(response.data)
  },

  async setAnnouncementEnabled(id: string, isEnabled: boolean): Promise<AdminAnnouncement> {
    const response = await http.patch<ApiResponse<AdminAnnouncement>>(`/api/v1/admin/announcements/${id}/enabled`, { isEnabled })
    return unwrapApiResponse(response.data)
  },

  async deleteAnnouncement(id: string): Promise<void> {
    const response = await http.delete<ApiResponse<{ id: string; deleted: boolean }>>(`/api/v1/admin/announcements/${id}`)
    unwrapApiResponse(response.data)
  },

  async listConversations(params?: { page?: number; pageSize?: number }): Promise<AdminConversationList> {
    const response = await http.get<ApiResponse<AdminConversationList>>('/api/v1/admin/conversations', { params })
    return unwrapApiResponse(response.data)
  },

  async getConversationMessages(
    conversationId: string,
    params?: { page?: number; pageSize?: number },
  ): Promise<AdminConversationMessages> {
    const response = await http.get<ApiResponse<AdminConversationMessages>>(
      `/api/v1/admin/conversations/${conversationId}/messages`,
      { params },
    )
    return unwrapApiResponse(response.data)
  },
}
