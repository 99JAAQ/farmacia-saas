import { useQuery } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useAuth } from '@/features/auth/useAuth'
import type { User } from '@/lib/api/types'
import { cashKeys, getCurrentCashSession, type CashSession } from './api'

interface Props {
  /** What needs the shift, to explain it: "vender", "hacer devoluciones". */
  action: string
  children: (session: CashSession, user: User) => ReactNode
}

/**
 * Selling and refunding move the cash of a shift, and only whoever opened it may touch it. Without a shift of one's
 * own, explains why and points to the register. The API enforces the same rule (403 / 409).
 */
export function RequireOwnShift({ action, children }: Props) {
  const { user } = useAuth()
  const current = useQuery({
    queryKey: cashKeys.current,
    queryFn: ({ signal }) => getCurrentCashSession(signal),
  })

  if (current.isPending) return <p className="text-sm text-muted-foreground">Cargando…</p>
  if (current.isError) return <p className="text-sm text-destructive">{current.error.message}</p>

  const { session } = current.data
  if (session === null || !session.isMine)
    return (
      <Card className="max-w-lg">
        <CardHeader>
          <CardTitle>{session === null ? 'La caja está cerrada' : `La caja está abierta por ${session.openedByName}`}</CardTitle>
          <CardDescription>
            {session === null
              ? `Para ${action}, primero abra su turno con la base de efectivo.`
              : `Solo quien abrió el turno puede ${action} en él. Si va a atender usted, ese turno debe cerrarse y usted abrir el suyo.`}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Link to="/caja" className={buttonVariants()}>
            Ir a Caja
          </Link>
        </CardContent>
      </Card>
    )

  return user && children(session, user)
}
