import { Navigate, Outlet, useLocation } from 'react-router'
import { FullScreenLoader } from '@/components/FullScreenLoader'
import { useAuth } from './useAuth'

/**
 * Layout route: renders its children only for a logged-in user; otherwise goes to the login, remembering the page.
 * A user with a temporary password goes to change it first: the API refuses everything else until then.
 */
export function RequireAuth() {
  const { status, user } = useAuth()
  const location = useLocation()

  if (status === 'restoring') return <FullScreenLoader label="Recuperando la sesión…" />

  if (status === 'anonymous')
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />

  if (user?.mustChangePassword && location.pathname !== '/cambiar-clave') return <Navigate to="/cambiar-clave" replace />

  return <Outlet />
}
