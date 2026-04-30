import { http } from '@/shared/api/http'
import { unwrapApiResponse, type ApiResponse } from '@/shared/types/api'

export type ReviewDetail = {
  reviewId: string
  purchaseRequestId: string
  reviewerId: string
  listingId: string
  sellerId: string
  buyerId: string
  rating: number
  content: string | null
  createdAt: string
}

export type ReviewStatusRaw = {
  purchaseRequestId: string
  buyerCanReview: boolean
  buyerReviewed: boolean
  buyerReviewBlockReason: string | null
  buyerReview: ReviewDetail | null
  sellerCanReview: boolean
  sellerReviewed: boolean
  sellerReviewBlockReason: string | null
  sellerReview: ReviewDetail | null
  viewerIsBuyer: boolean
  viewerCanReview: boolean
  viewerReviewed: boolean
  viewerReviewBlockReason: string | null
  viewerReview: ReviewDetail | null
}

/** 評價狀態（含與舊版相容的 viewer 別名欄位）。 */
export type ReviewStatus = ReviewStatusRaw & {
  canReview: boolean
  reviewed: boolean
  reason: string | null
  review: ReviewDetail | null
}

type CreateReviewPayload = {
  rating: number
  content?: string | null
}

const normalizeReviewStatus = (data: ReviewStatusRaw): ReviewStatus => ({
  ...data,
  canReview: data.viewerCanReview,
  reviewed: data.viewerReviewed,
  reason: data.viewerReviewBlockReason,
  review: data.viewerReview,
})

export const reviewApi = {
  async getStatus(purchaseRequestId: string): Promise<ReviewStatus> {
    const response = await http.get<ApiResponse<ReviewStatusRaw>>(
      `/api/v1/purchase-requests/${purchaseRequestId}/review-status`,
    )
    return normalizeReviewStatus(unwrapApiResponse(response.data))
  },

  async create(purchaseRequestId: string, payload: CreateReviewPayload): Promise<ReviewDetail> {
    const response = await http.post<ApiResponse<ReviewDetail>>(`/api/v1/purchase-requests/${purchaseRequestId}/reviews`, payload)
    return unwrapApiResponse(response.data)
  },
}
