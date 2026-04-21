import { http } from '@/shared/api/http'
import { unwrapApiResponse, type ApiResponse } from '@/shared/types/api'

export type LookupItem = {
  id: number
  codeKey: string
  displayName: string
  sortOrder: number
}

const getLookup = async (path: string): Promise<LookupItem[]> => {
  const response = await http.get<ApiResponse<LookupItem[]>>(path)
  return unwrapApiResponse(response.data)
}

export const lookupApi = {
  categories: () => getLookup('/api/v1/lookups/categories'),
  conditions: () => getLookup('/api/v1/lookups/conditions'),
  residences: () => getLookup('/api/v1/lookups/residences'),
  pickupLocations: () => getLookup('/api/v1/lookups/pickup-locations'),
}
