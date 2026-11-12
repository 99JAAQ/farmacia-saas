import { api, setSession } from '@/lib/api/http'
import type { Session } from '@/lib/api/types'

export interface Credentials {
  email: string
  password: string
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

/**
 * The signed-in user's own password (also a temporary one). The API ends the user's other sessions and returns a
 * new one, which replaces the current session here (with mustChangePassword false).
 */
export async function changePassword(currentPassword: string, newPassword: string): Promise<Session> {
  const session = await api<Session>('/api/auth/change-password', { method: 'POST', body: { currentPassword, newPassword } })
  setSession(session)
  return session
}
