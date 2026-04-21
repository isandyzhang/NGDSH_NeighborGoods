export type AuthTokens = {
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
  userId: string
}

export type LoginPayload = {
  userNameOrEmail: string
  password: string
}
