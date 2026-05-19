const isSafeInternalPath = (path: string) => path.startsWith('/') && !path.startsWith('//')

const parseLiffStateTarget = (liffState: string): string | null => {
  const decoded = decodeURIComponent(liffState.trim())
  if (!decoded) {
    return null
  }

  const normalized = decoded.startsWith('?') ? decoded.slice(1) : decoded
  const qIndex = normalized.indexOf('?')
  const path = qIndex >= 0 ? normalized.slice(0, qIndex) : normalized
  const query = qIndex >= 0 ? normalized.slice(qIndex) : ''

  if (!isSafeInternalPath(path)) {
    return null
  }

  return `${path}${query}`
}

export const resolveLiffEntryTarget = (pathname: string, search: string): string | null => {
  const params = new URLSearchParams(search)

  const liffState = params.get('liff.state')
  if (liffState) {
    const target = parseLiffStateTarget(liffState)
    if (target) {
      return target
    }
  }

  const bindToken = params.get('bindToken')
  if (bindToken && pathname === '/') {
    const botLink = params.get('botLink') ?? ''
    return `/liff/line-notify?bindToken=${encodeURIComponent(bindToken)}&botLink=${encodeURIComponent(botLink)}`
  }

  return null
}
