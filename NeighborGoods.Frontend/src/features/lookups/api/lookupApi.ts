import { http } from '@/shared/api/http'
import { unwrapApiResponse, type ApiResponse } from '@/shared/types/api'

export type LookupItem = {
  id: number
  codeKey: string
  displayName: string
  sortOrder: number
  residenceId?: number | null
}

const getLookup = async (path: string, params?: Record<string, string | number | undefined>): Promise<LookupItem[]> => {
  const response = await http.get<ApiResponse<LookupItem[]>>(path, { params })
  return unwrapApiResponse(response.data)
}

export const lookupApi = {
  categories: () => getLookup('/api/v1/lookups/categories'),
  conditions: () => getLookup('/api/v1/lookups/conditions'),
  residences: () => getLookup('/api/v1/lookups/residences'),
  pickupLocations: (residenceId?: number | null) =>
    getLookup('/api/v1/lookups/pickup-locations', {
      residenceId: residenceId ?? 0,
    }),
}
