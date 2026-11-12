import { ChevronRight } from 'lucide-react'
import { Link } from 'react-router'
import { sectionsFor } from '@/components/navigation'
import { can } from '@/features/auth/permissions'
import { useAuth } from '@/features/auth/useAuth'
import { DailySummaryPanel } from '@/features/dashboard/DailySummaryPanel'

/** Greeting, the day's live summary (owner only) and a shortcut to each section the user may use. */
export function HomePage() {
  const { user } = useAuth()
  const shortcuts = sectionsFor(user).filter((section) => section.to !== '/')

  return (
    <div className="grid gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Hola, {user?.fullName}</h1>
        <p className="text-muted-foreground">¿Qué vamos a hacer hoy?</p>
      </div>
      {can(user, 'viewDashboard') && <DailySummaryPanel />}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {shortcuts.map(({ to, label, description, icon: Icon }) => (
          <Link
            key={to}
            to={to}
            className="group flex items-center gap-4 rounded-xl border bg-card p-4 shadow-xs transition-colors hover:border-primary/40 hover:bg-accent/40 focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none"
          >
            <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-accent text-accent-foreground">
              <Icon className="size-5" aria-hidden="true" />
            </span>
            <span className="grid flex-1 gap-0.5">
              <span className="font-medium">{label}</span>
              <span className="text-sm text-muted-foreground">{description}</span>
            </span>
            <ChevronRight className="size-4 text-muted-foreground transition-transform group-hover:translate-x-0.5" aria-hidden="true" />
          </Link>
        ))}
      </div>
    </div>
  )
}
