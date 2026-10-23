import { Outlet } from 'react-router'
import { can, type Policy } from './permissions'
import { useAuth } from './useAuth'

/**
 * Layout route for pages that need a policy. Shows a clear message instead of redirecting, so the user
 * understands what happened. The API enforces the same policy: this only avoids showing a page that would fail.
 */
export function RequireRole({ policy }: { policy: Policy }) {
  const { user } = useAuth()

  if (!can(user, policy))
    return (
      <section className="mx-auto max-w-md py-16 text-center">
        <h1 className="text-lg font-semibold">No tiene permiso</h1>
        <p className="mt-2 text-sm text-muted-foreground">Su rol no permite usar esta sección. Si la necesita, pídale acceso al dueño de la farmacia.</p>
      </section>
    )

  return <Outlet />
}
