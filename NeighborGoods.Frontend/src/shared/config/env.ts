const normalizeBaseUrl = (value: string | undefined, fallback: string) => {
  if (!value) {
    return fallback
  }

  return value.endsWith('/') ? value.slice(0, -1) : value
}

export const env = {
  apiBaseUrl: normalizeBaseUrl(import.meta.env.VITE_API_BASE_URL, 'http://localhost:5065'),
  signalrBaseUrl: normalizeBaseUrl(
    import.meta.env.VITE_SIGNALR_BASE_URL,
    import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5065',
  ),
}
