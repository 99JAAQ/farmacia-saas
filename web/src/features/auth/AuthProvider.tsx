import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { onSessionChange, refreshSession } from '@/lib/api/http'
import type { User } from '@/lib/api/types'
import { login, logout } from './api'
import { AuthContext, type AuthContextValue, type AuthStatus } from './AuthContext'

interface AuthState {
  status: AuthStatus
  user: User | null
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [state, setState] = useState<AuthState>({ status: 'restoring', user: null })

  useEffect(() => {
    // The HTTP client is the single source of truth for the session (login, refresh, logout, expiration).
    const unsubscribe = onSessionChange((session) => {
      setState(session ? { status: 'authenticated', user: session.user } : { status: 'anonymous', user: null })
      // Never show one user's cached data to the next user of the same computer.
      if (!session) queryClient.clear()
    })

    // Restore the session after a reload: the access token died with the previous page, the HttpOnly cookie did not.
    // In development StrictMode runs this effect twice; the single-flight refresh makes both share one request.
    refreshSession().catch(() => {
      // The API could not be reached: show the login screen, which reports the connection problem on submit.
      setState((current) => (current.status === 'restoring' ? { status: 'anonymous', user: null } : current))
    })

    return unsubscribe
  }, [queryClient])

  const value = useMemo<AuthContextValue>(
    () => ({
      ...state,
      login: async (credentials) => {
        await login(credentials)
      },
      logout,
    }),
    [state],
  )

  return <AuthContext value={value}>{children}</AuthContext>
}
