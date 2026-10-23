import { Navigate, Outlet, useLocation } from 'react-router'
import { FullScreenLoader } from '@/components/FullScreenLoader'
import { useAuth } from './useAuth'

/** Layout route: renders its children only for a logged-in user; otherwise goes to the login, remembering the page. */
export function RequireAuth() {
  const { status } = useAuth()
  const location = useLocation()

  if (status === 'restoring') return <FullScreenLoader label="Recuperando la sesión…" />

  if (status === 'anonymous')
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />

  return <Outlet />
}
