import { http } from '@/shared/api/http'
import { unwrapApiResponse, type ApiResponse } from '@/shared/types/api'

export type ReviewDetail = {
  reviewId: string
  listingId: string
  sellerId: string
  buyerId: string
  rating: number
  content: string | null
  createdAt: string
}

export type ReviewStatus = {
  purchaseRequestId: string
  canReview: boolean
  reviewed: boolean
  reason: string | null
  review: ReviewDetail | null
}

type CreateReviewPayload = {
  rating: number
  content?: string | null
}

export const reviewApi = {
  async getStatus(purchaseRequestId: string): Promise<ReviewStatus> {
    const response = await http.get<ApiResponse<ReviewStatus>>(`/api/v1/purchase-requests/${purchaseRequestId}/review-status`)
    return unwrapApiResponse(response.data)
  },

  async create(purchaseRequestId: string, payload: CreateReviewPayload): Promise<ReviewDetail> {
    const response = await http.post<ApiResponse<ReviewDetail>>(`/api/v1/purchase-requests/${purchaseRequestId}/reviews`, payload)
    return unwrapApiResponse(response.data)
  },
}
