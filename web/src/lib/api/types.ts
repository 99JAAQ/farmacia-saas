export type Role = 'Owner' | 'Cashier' | 'StockKeeper'

export interface User {
  id: string
  email: string
  fullName: string
  pharmacyId: string
  roles: Role[]
}

/** Response of /api/auth/login and /api/auth/refresh. The refresh token is not here: it travels in an HttpOnly cookie. */
export interface Session {
  accessToken: string
  accessTokenExpiresAt: string
  user: User
}

/** Error body returned by the API (RFC 9457). */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}
