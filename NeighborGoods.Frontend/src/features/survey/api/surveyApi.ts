import { http } from '@/shared/api/http'
import { unwrapApiResponse, type ApiResponse } from '@/shared/types/api'

export type SubmitSurveyPayload = {
  residentType: 'social' | 'non_social'
  displayName?: string | null
  phone?: string | null
  building?: 'south' | 'north' | null
  floor?: number | null
  ratingUi: 'satisfied' | 'fair' | 'unsatisfied'
  ratingListing: 'satisfied' | 'fair' | 'unsatisfied'
  ratingInteraction: 'satisfied' | 'fair' | 'unsatisfied'
  ratingGuide: 'satisfied' | 'fair' | 'unsatisfied'
  ratingPromo: 'satisfied' | 'fair' | 'unsatisfied'
  channel: 'friends' | 'bulletin' | 'youth_online' | 'other'
  feedback?: string | null
  consentAccepted: boolean
}

export type SurveySubmitResult = {
  id: string
  createdAt: string
  isGiftEligible: boolean
  remainingGiftSlots: number
}

export type SurveyGiftStatus = {
  giftName: string
  pickupNote: string
  giftLimit: number
  remainingGiftSlots: number
  isAvailable: boolean
  giftListingPath: string | null
  alreadySubmitted: boolean
  alreadyGiftEligible: boolean
}

export const surveyApi = {
  async getGiftStatus(): Promise<SurveyGiftStatus> {
    const response = await http.get<ApiResponse<SurveyGiftStatus>>('/api/v1/surveys/gift-status')
    return unwrapApiResponse(response.data)
  },

  async submit(payload: SubmitSurveyPayload): Promise<SurveySubmitResult> {
    const response = await http.post<ApiResponse<SurveySubmitResult>>('/api/v1/surveys', payload)
    return unwrapApiResponse(response.data)
  },
}
