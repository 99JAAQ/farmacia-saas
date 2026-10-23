import { NavLink, Outlet } from 'react-router'
import { Button } from '@/components/ui/button'
import { can } from '@/features/auth/permissions'
import { roleLabels } from '@/features/auth/roles'
import { useAuth } from '@/features/auth/useAuth'
import { cn } from '@/lib/utils'

export function AppLayout() {
  const { user, logout } = useAuth()

  const links = [
    { to: '/', label: 'Inicio', visible: true },
    { to: '/catalogo', label: 'Catálogo', visible: can(user, 'manageCatalog') },
  ].filter((link) => link.visible)

  return (
    <div className="min-h-svh bg-muted">
      <header className="border-b bg-background">
        <div className="mx-auto flex max-w-6xl items-center gap-6 px-4 py-3">
          <span className="font-semibold">Farmacia</span>
          <nav className="flex gap-1">
            {links.map((link) => (
              <NavLink
                key={link.to}
                to={link.to}
                end
                className={({ isActive }) =>
                  cn('rounded-md px-3 py-1.5 text-sm hover:bg-muted', isActive && 'bg-muted font-medium')
                }
              >
                {link.label}
              </NavLink>
            ))}
          </nav>
          <div className="ml-auto flex items-center gap-3">
            <div className="text-right text-sm leading-tight">
              <div className="font-medium">{user?.fullName}</div>
              <div className="text-muted-foreground">{user?.roles.map((role) => roleLabels[role]).join(', ')}</div>
            </div>
            <Button variant="outline" size="sm" onClick={() => void logout()}>
              Cerrar sesión
            </Button>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-6xl px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}
