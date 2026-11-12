import { LogOut } from 'lucide-react'
import { usePharmacy } from '@/features/pharmacy/usePharmacy'
import { NavLink, Outlet } from 'react-router'
import { Button } from '@/components/ui/button'
import { roleLabels } from '@/features/auth/roles'
import { useAuth } from '@/features/auth/useAuth'
import { cn } from '@/lib/utils'
import { Logo } from './Logo'
import { sectionsFor } from './navigation'

/**
 * Sidebar on wide screens (the register's computer); a top bar with scrollable links on narrow ones.
 * Both read the same sections, filtered by the user's permissions.
 */
export function AppLayout() {
  const { user, logout } = useAuth()
  const sections = sectionsFor(user)
  const pharmacy = usePharmacy()
  const roles = user?.roles.map((role) => roleLabels[role]).join(', ')

  const links = sections.map(({ to, label, icon: Icon }) => (
    <NavLink
      key={to}
      to={to}
      end={to === '/'} // "Compras" stays highlighted on /compras/nueva too
      className={({ isActive }) =>
        cn(
          'flex shrink-0 items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium text-sidebar-foreground/75 transition-colors',
          'hover:bg-sidebar-accent hover:text-sidebar-accent-foreground',
          isActive && 'bg-sidebar-accent text-sidebar-accent-foreground',
        )
      }
    >
      <Icon className="size-4" aria-hidden="true" />
      {label}
    </NavLink>
  ))

  return (
    <div className="min-h-svh bg-muted md:grid md:grid-cols-[15rem_1fr]">
      <aside className="hidden border-r bg-sidebar md:sticky md:top-0 md:flex md:h-svh md:flex-col">
        <div className="grid gap-1 px-5 py-5">
          <Logo />
          {/* Which pharmacy this session works in: with several on the platform, it must be obvious. */}
          {pharmacy.data && <div className="truncate pl-[2.625rem] text-xs text-muted-foreground">{pharmacy.data.name}</div>}
        </div>
        <nav aria-label="Secciones" className="grid gap-1 px-3">
          {links}
        </nav>
        <div className="mt-auto border-t p-4">
          <div className="truncate text-sm font-medium">{user?.fullName}</div>
          <div className="truncate text-xs text-muted-foreground">{roles}</div>
          <Button variant="outline" size="sm" className="mt-3 w-full" onClick={() => void logout()}>
            <LogOut aria-hidden="true" />
            Cerrar sesión
          </Button>
        </div>
      </aside>

      <header className="border-b bg-sidebar md:hidden">
        <div className="flex items-center justify-between px-4 py-3">
          <Logo />
          <Button variant="outline" size="sm" onClick={() => void logout()}>
            <LogOut aria-hidden="true" />
            Salir
          </Button>
        </div>
        <nav aria-label="Secciones" className="flex gap-1 overflow-x-auto px-3 pb-2">
          {links}
        </nav>
      </header>

      <main className="mx-auto w-full max-w-6xl px-4 py-6 md:px-8 md:py-8">
        <Outlet />
      </main>
    </div>
  )
}
