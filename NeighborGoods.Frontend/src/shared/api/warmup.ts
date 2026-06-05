import { env } from '@/shared/config/env'

const PING_PATH = '/api/v1/ping'
const WARMUP_REQUEST_TIMEOUT_MS = 90_000

export const pingApi = async (timeoutMs = WARMUP_REQUEST_TIMEOUT_MS): Promise<boolean> => {
  const controller = new AbortController()
  const timer = window.setTimeout(() => controller.abort(), timeoutMs)

  try {
    const response = await fetch(`${env.apiBaseUrl}${PING_PATH}`, {
      method: 'GET',
      signal: controller.signal,
    })
    return response.ok
  } catch {
    return false
  } finally {
    window.clearTimeout(timer)
  }
}
