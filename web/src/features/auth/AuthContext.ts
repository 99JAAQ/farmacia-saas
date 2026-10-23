import { createContext } from 'react'
import type { User } from '@/lib/api/types'
import type { Credentials } from './api'

/** "restoring": the page just loaded and the session is being recovered from the refresh cookie. */
export type AuthStatus = 'restoring' | 'authenticated' | 'anonymous'

export interface AuthContextValue {
  status: AuthStatus
  /** null unless status is "authenticated". */
  user: User | null
  login: (credentials: Credentials) => Promise<void>
  logout: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue | null>(null)
