/** 與 GitHub Actions `frontend_cd_swa`、後端 LineMessagingApi:LiffId 一致 */
export const PRODUCTION_LINE_LIFF_ID = '2008745853-Ui8PkOGi'

/** build 時內嵌的 VITE_LINE_LIFF_ID；production 建置若漏設 env 則用常數 fallback */
export const resolveLineLiffId = (): string | null => {
  const fromEnv = import.meta.env.VITE_LINE_LIFF_ID as string | undefined
  const trimmed = fromEnv?.trim()
  if (trimmed) {
    return trimmed
  }
  if (import.meta.env.PROD) {
    return PRODUCTION_LINE_LIFF_ID
  }
  return null
}
