export type ApiMeta = {
  traceId: string
  timestamp: string
}

export type ApiErrorBody = {
  code: string
  message: string
  details?: unknown
}

export type ApiSuccessResponse<T> = {
  success: true
  data: T
  meta: ApiMeta
}

export type ApiErrorResponse = {
  success: false
  error: ApiErrorBody
  meta: ApiMeta
}

export type ApiResponse<T> = ApiSuccessResponse<T> | ApiErrorResponse

export class ApiClientError extends Error {
  code: string

  status?: number

  constructor(message: string, code = 'API_ERROR', status?: number) {
    super(message)
    this.name = 'ApiClientError'
    this.code = code
    this.status = status
  }
}

export const unwrapApiResponse = <T>(response: ApiResponse<T>): T => {
  if (response.success) {
    return response.data
  }

  throw new ApiClientError(response.error.message, response.error.code)
}
