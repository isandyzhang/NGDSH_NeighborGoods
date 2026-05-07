export type AuthTokens = {
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
  userId: string
  role: number
}

export type LoginPayload = {
  userNameOrEmail: string
  password: string
}
