/** PlatformAdmin creates pharmacies and belongs to none (pharmacyId null). */
export type Role = 'Owner' | 'Cashier' | 'StockKeeper' | 'PlatformAdmin'

export interface User {
  id: string
  email: string
  fullName: string
  /** Null only for the platform administrator. */
  pharmacyId: string | null
  roles: Role[]
  /** Has a temporary password: the API refuses everything but changing it (the app takes the user there). */
  mustChangePassword: boolean
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
