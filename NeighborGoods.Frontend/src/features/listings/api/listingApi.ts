import { http } from '@/shared/api/http'
import { unwrapApiResponse, type ApiResponse } from '@/shared/types/api'

export type ListingItem = {
  id: string
  sellerId: string
  title: string
  sellerDisplayName: string
  sellerEmailVerified: boolean
  sellerEmailNotificationEnabled: boolean
  sellerLineLoginBound: boolean
  sellerLineNotifyBound: boolean
  sellerQuickResponder: boolean
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
  pendingPurchaseRequestExpireAt: string | null
  pendingPurchaseRequestRemainingSeconds: number | null
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

export type MyListingItem = {
  id: string
  title: string
  categoryCode: number
  categoryName: string
  price: number
  isFree: boolean
  isCharity: boolean
  isTradeable: boolean
  statusCode: number
  mainImageUrl: string | null
  createdAt: string
  updatedAt: string
}

type MyListPayload = {
  items: MyListingItem[]
  pagination: {
    page: number
    pageSize: number
    totalCount: number
    totalPages: number
  }
}

export type FavoriteListingItem = {
  listingId: string
  title: string
  categoryCode: number
  categoryName: string
  price: number
  isFree: boolean
  mainImageUrl: string | null
  favoritedAt: string
}

type FavoriteListPayload = {
  items: FavoriteListingItem[]
  pagination: {
    page: number
    pageSize: number
    totalCount: number
    totalPages: number
  }
}

export type SellerSummary = {
  sellerId: string
  sellerDisplayName: string
  totalListings: number
  activeListings: number
  completedListings: number
}

export type SellerListingItem = {
  id: string
  title: string
  categoryCode: number
  categoryName: string
  price: number
  isFree: boolean
  statusCode: number
  mainImageUrl: string | null
  createdAt: string
}

export type InterestCategory = {
  categoryCode: number
  categoryName: string
  score: number
  favoriteCount: number
}

export type InterestProfile = {
  userId: string
  windowDays: number
  topCategories: InterestCategory[]
  updatedAt: string
}

type SellerListingsPayload = {
  seller: SellerSummary
  items: SellerListingItem[]
  pagination: {
    page: number
    pageSize: number
    totalCount: number
    totalPages: number
  }
}

export type FavoriteTogglePayload = {
  listingId: string
  isFavorited: boolean
  favoriteCount: number
  favoritedAt: string | null
}

export type FavoriteStatusPayload = {
  listingId: string
  favoriteCount: number
  isFavorited: boolean
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
  pendingPurchaseRequestExpireAt: string | null
  pendingPurchaseRequestRemainingSeconds: number | null
  createdAt: string
  updatedAt: string | null
}

export type ListingMutationPayload = {
  title: string
  description: string
  categoryCode: number
  conditionCode: number
  price: number
  residenceCode: number
  pickupLocationCode: number
  isFree: boolean
  isCharity: boolean
  isTradeable: boolean
}

type ListingStatusAction =
  | 'reserve'
  | 'activate'
  | 'sold'
  | 'inactive'
  | 'donated'
  | 'given-or-traded'
  | 'reactivate'

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

  async listMine(page = 1, pageSize = 20): Promise<MyListPayload> {
    const response = await http.get<ApiResponse<MyListPayload>>('/api/v1/listings/mine', {
      params: { page, pageSize },
    })
    return unwrapApiResponse(response.data)
  },

  async listFavorites(page = 1, pageSize = 20, categoryCode?: number): Promise<FavoriteListPayload> {
    const response = await http.get<ApiResponse<FavoriteListPayload>>('/api/v1/listings/favorites', {
      params: { page, pageSize, categoryCode },
    })
    return unwrapApiResponse(response.data)
  },

  async getSellerListings(sellerId: string, page = 1, pageSize = 20): Promise<SellerListingsPayload> {
    const response = await http.get<ApiResponse<SellerListingsPayload>>(`/api/v1/sellers/${sellerId}/listings`, {
      params: { page, pageSize },
    })
    return unwrapApiResponse(response.data)
  },

  async getInterestProfile(days = 90, topN = 5): Promise<InterestProfile> {
    const response = await http.get<ApiResponse<InterestProfile>>('/api/v1/users/me/interest-profile', {
      params: { days, topN },
    })
    return unwrapApiResponse(response.data)
  },

  async create(payload: ListingMutationPayload, images: File[], useTopPin = false): Promise<{ id: string }> {
    const formData = new FormData()
    formData.append(
      'payload',
      JSON.stringify({
        ...payload,
        description: payload.description.trim() || null,
        useTopPin,
      }),
    )
    images.forEach((file) => formData.append('images', file))

    const response = await http.post<ApiResponse<{ id: string }>>('/api/v1/listings', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return unwrapApiResponse(response.data)
  },

  async update(
    id: string,
    payload: ListingMutationPayload,
    imageUrlsToDelete: string[] = [],
    imageUrlsInOrder: string[] = [],
  ): Promise<{ id: string }> {
    const response = await http.put<ApiResponse<{ id: string }>>(`/api/v1/listings/${id}`, {
      ...payload,
      description: payload.description.trim() || null,
      imageUrlsToDelete,
      imageUrlsInOrder,
    })
    return unwrapApiResponse(response.data)
  },

  async addImage(id: string, file: File): Promise<{ imageId: string; sortOrder: number; blobName: string }> {
    const formData = new FormData()
    formData.append('file', file)
    const response = await http.post<ApiResponse<{ imageId: string; sortOrder: number; blobName: string }>>(
      `/api/v1/listings/${id}/images`,
      formData,
      { headers: { 'Content-Type': 'multipart/form-data' } },
    )
    return unwrapApiResponse(response.data)
  },

  async changeStatus(id: string, action: ListingStatusAction): Promise<{ id: string; warning?: string | null }> {
    const response = await http.patch<ApiResponse<{ id: string; warning?: string | null }>>(
      `/api/v1/listings/${id}/${action}`,
    )
    return unwrapApiResponse(response.data)
  },

  async favorite(id: string): Promise<FavoriteTogglePayload> {
    const response = await http.post<ApiResponse<FavoriteTogglePayload>>(`/api/v1/listings/${id}/favorite`)
    return unwrapApiResponse(response.data)
  },

  async unfavorite(id: string): Promise<FavoriteTogglePayload> {
    const response = await http.delete<ApiResponse<FavoriteTogglePayload>>(`/api/v1/listings/${id}/favorite`)
    return unwrapApiResponse(response.data)
  },

  async getFavoriteStatus(id: string): Promise<FavoriteStatusPayload> {
    const response = await http.get<ApiResponse<FavoriteStatusPayload>>(`/api/v1/listings/${id}/favorite-status`)
    return unwrapApiResponse(response.data)
  },
}
