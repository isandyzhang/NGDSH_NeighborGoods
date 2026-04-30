const normalizeBaseUrl = (value: string | undefined, fallback: string) => {
  if (!value) {
    return fallback
  }

  return value.endsWith('/') ? value.slice(0, -1) : value
}

const getRequiredEnv = (key: 'VITE_API_BASE_URL' | 'VITE_SIGNALR_BASE_URL') => {
  const value = import.meta.env[key]

  if (import.meta.env.PROD && !value) {
    throw new Error(`[env] Missing required environment variable: ${key}`)
  }

  return value
}

const apiBaseUrl = normalizeBaseUrl(getRequiredEnv('VITE_API_BASE_URL'), 'http://localhost:5065')

export const env = {
  apiBaseUrl,
  signalrBaseUrl: normalizeBaseUrl(
    getRequiredEnv('VITE_SIGNALR_BASE_URL'),
    apiBaseUrl,
  ),
}
