const normalizeBaseUrl = (value: string | undefined, fallback: string) => {
  if (!value) {
    return fallback
  }

  return value.endsWith('/') ? value.slice(0, -1) : value
}

// 必須以「靜態」讀取 import.meta.env.VITE_*，Vite 才能在 build 時把字面量內嵌；
// 使用 import.meta.env[key] 動態鍵時，產物不會帶入自訂變數，正式站會永遠拋錯。
const apiEnv = import.meta.env.VITE_API_BASE_URL
const signalEnv = import.meta.env.VITE_SIGNALR_BASE_URL

if (import.meta.env.PROD && !apiEnv) {
  throw new Error('[env] Missing required environment variable: VITE_API_BASE_URL')
}

if (import.meta.env.PROD && !signalEnv) {
  throw new Error('[env] Missing required environment variable: VITE_SIGNALR_BASE_URL')
}

const apiBaseUrl = normalizeBaseUrl(apiEnv, 'http://localhost:5065')

export const env = {
  apiBaseUrl,
  signalrBaseUrl: normalizeBaseUrl(signalEnv, apiBaseUrl),
}
