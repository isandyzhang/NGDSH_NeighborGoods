import { http } from '@/shared/api/http'
import { unwrapApiResponse, type ApiResponse } from '@/shared/types/api'

export type ListingItem = {
  id: string
  title: string
  categoryCode: number
  categoryName: string
  conditionCode: number
  conditionName: string
  price: number
  residenceCode: number
  residenceName: string
  mainImageUrl: string | null
  statusCode: number
  isFree: boolean
  isCharity: boolean
  isTradeable: boolean
  isPinned: boolean
  pinnedEndDate: string | null
  interestCount: number
}

type ListPayload = {
  items: ListingItem[]
  pagination: {
    page: number
    pageSize: number
    totalCount: number
    totalPages: number
  }
}

export type ListingDetail = {
  id: string
  title: string
  description: string | null
  categoryCode: number
  categoryName: string
  conditionCode: number
  conditionName: string
  price: number
  residenceCode: number
  residenceName: string
  pickupLocationCode: number
  pickupLocationName: string
  mainImageUrl: string | null
  imageUrls: string[]
  statusCode: number
  isFree: boolean
  isCharity: boolean
  isTradeable: boolean
  isPinned: boolean
  pinnedStartDate: string | null
  pinnedEndDate: string | null
  createdAt: string
  updatedAt: string | null
}

type QueryParams = {
  q?: string
  page?: number
  pageSize?: number
  categoryCode?: number
  conditionCode?: number
  residenceCode?: number
  categoryCodes?: number[]
  conditionCodes?: number[]
  residenceCodes?: number[]
  isFree?: boolean
  isCharity?: boolean
  isTradeable?: boolean
  minPrice?: number
  maxPrice?: number
}

export const listingApi = {
  async list(params: QueryParams): Promise<ListPayload> {
    const normalizedParams = {
      ...params,
      categoryCodes: params.categoryCodes?.length ? params.categoryCodes.join(',') : undefined,
      conditionCodes: params.conditionCodes?.length ? params.conditionCodes.join(',') : undefined,
      residenceCodes: params.residenceCodes?.length ? params.residenceCodes.join(',') : undefined,
    }

    const response = await http.get<ApiResponse<ListPayload>>('/api/v1/listings', {
      params: normalizedParams,
    })
    return unwrapApiResponse(response.data)
  },

  async getById(id: string): Promise<ListingDetail> {
    const response = await http.get<ApiResponse<ListingDetail>>(`/api/v1/listings/${id}`)
    return unwrapApiResponse(response.data)
  },
}
