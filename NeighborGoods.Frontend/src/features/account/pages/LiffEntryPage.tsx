import { Navigate, useSearchParams } from 'react-router-dom'

const FALLBACK_PATH = '/listings'

const decodeSafely = (value: string) => {
  try {
    return decodeURIComponent(value)
  } catch {
    return value
  }
}

const resolveTarget = (rawTarget: string) => {
  if (!rawTarget) {
    return ''
  }

  const decoded = decodeSafely(rawTarget).trim()
  if (!decoded) {
    return ''
  }

  if (decoded.startsWith('http://') || decoded.startsWith('https://')) {
    try {
      const u = new URL(decoded)
      return `${u.pathname}${u.search}`
    } catch {
      return ''
    }
  }

  return decoded
}

const isSafeLiffPath = (target: string) => target.startsWith('/liff/') && !target.includes('://')

export const LiffEntryPage = () => {
  const [searchParams] = useSearchParams()

  const targetRaw = searchParams.get('target') ?? searchParams.get('liff.state') ?? ''
  const target = resolveTarget(targetRaw)
  const nextPath = isSafeLiffPath(target) ? target : FALLBACK_PATH

  return <Navigate to={nextPath} replace />
}
