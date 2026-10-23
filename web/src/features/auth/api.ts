import { api, setSession } from '@/lib/api/http'
import type { Role, Session } from '@/lib/api/types'

export interface Credentials {
  email: string
  password: string
}

/** What /api/auth/me reads from the access token: proves the token is accepted by the API. */
export interface CurrentUser {
  userId: string
  email: string
  fullName: string
  pharmacyId: string
  roles: Role[]
}

export async function login(credentials: Credentials): Promise<Session> {
  const session = await api<Session>('/api/auth/login', { method: 'POST', body: credentials, skipRefresh: true })
  setSession(session)
  return session
}

export async function logout(): Promise<void> {
  try {
    await api<void>('/api/auth/logout', { method: 'POST', skipRefresh: true })
  } finally {
    // Even if the server cannot be reached, this device forgets the session.
    setSession(null)
  }
}

export function getCurrentUser(signal?: AbortSignal): Promise<CurrentUser> {
  return api<CurrentUser>('/api/auth/me', { signal })
}
