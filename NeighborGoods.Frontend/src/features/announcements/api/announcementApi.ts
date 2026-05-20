import { http } from '@/shared/api/http'
import { unwrapApiResponse, type ApiResponse } from '@/shared/types/api'

export type ActiveAnnouncement = {
  id: string
  message: string
  severity: number
  scope: number
  linkUrl: string | null
  linkLabel: string | null
}

export const announcementApi = {
  async getActive(scope?: number): Promise<ActiveAnnouncement[]> {
    const response = await http.get<ApiResponse<{ items: ActiveAnnouncement[] }>>('/api/v1/announcements/active', {
      params: scope === undefined ? undefined : { scope },
    })
    return unwrapApiResponse(response.data).items
  },
}
