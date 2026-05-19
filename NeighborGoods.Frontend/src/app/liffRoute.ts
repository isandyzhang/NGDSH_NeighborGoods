const isSafeInternalPath = (path: string) => path.startsWith('/') && !path.startsWith('//')

const buildLineNotifyTarget = (bindToken: string, botLink: string) =>
  `/liff/line-notify?bindToken=${encodeURIComponent(bindToken)}&botLink=${encodeURIComponent(botLink)}`

const parseLiffStateTarget = (liffState: string): string | null => {
  const decoded = decodeURIComponent(liffState.trim())
  if (!decoded) {
    return null
  }

  const normalized = decoded.startsWith('?') ? decoded.slice(1) : decoded
  const qIndex = normalized.indexOf('?')
  const path = qIndex >= 0 ? normalized.slice(0, qIndex) : normalized
  const query = qIndex >= 0 ? normalized.slice(qIndex) : ''

  if (isSafeInternalPath(path)) {
    return `${path}${query}`
  }

  // liff.state may carry query-only params (e.g. bindToken=...&botLink=...)
  const params = new URLSearchParams(normalized)
  const bindToken = params.get('bindToken')
  if (bindToken) {
    return buildLineNotifyTarget(bindToken, params.get('botLink') ?? '')
  }

  return null
}

export const resolveLiffEntryTarget = (pathname: string, search: string): string | null => {
  const params = new URLSearchParams(search)
  const bindToken = params.get('bindToken')
  const botLink = params.get('botLink') ?? ''

  // Binding: outer bindToken first (LINE may truncate liff.state).
  if (bindToken && (pathname === '/' || pathname === '/liff')) {
    return buildLineNotifyTarget(bindToken, botLink)
  }

  const liffState = params.get('liff.state')
  if (liffState) {
    const target = parseLiffStateTarget(liffState)
    if (target) {
      return target
    }
  }

  return null
}
