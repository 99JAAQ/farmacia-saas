import { useQuery } from '@tanstack/react-query'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatMoney, formatTime } from '@/lib/format'
import { cn } from '@/lib/utils'
import { cashKeys, getCurrentCashSession, type CashSession } from './api'
import { CloseShiftDialog } from './CloseShiftDialog'
import { movementTypeLabels } from './labels'
import { MovementForm } from './MovementForm'
import { OpenShiftForm } from './OpenShiftForm'

export function CashPage() {
  const current = useQuery({
    queryKey: cashKeys.current,
    queryFn: ({ signal }) => getCurrentCashSession(signal),
  })

  if (current.isPending) return <p className="text-sm text-muted-foreground">Cargando…</p>
  if (current.isError) return <p className="text-sm text-destructive">{current.error.message}</p>

  const { session, suggestedOpeningFloat } = current.data

  return (
    <div className="grid gap-6">
      <h1 className="text-2xl font-semibold">Caja</h1>
      {session === null ? (
        <OpenShiftForm key={suggestedOpeningFloat} suggestedOpeningFloat={suggestedOpeningFloat} />
      ) : (
        <ShiftView session={session} />
      )}
    </div>
  )
}

function ShiftView({ session }: { session: CashSession }) {
  // The server sends the expected cash only to the owner while the shift is open.
  const canSeeExpected = session.expectedCash !== null

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          {session.isMine ? 'Su turno' : `Turno de ${session.openedByName}`} · desde las {formatTime(session.openedAt)}
        </CardTitle>
        <CardDescription>Base inicial: {formatMoney(session.openingFloat)}</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-6">
        {!session.isMine && (
          <Alert>
            <AlertDescription>
              La caja está abierta por {session.openedByName}. Solo esa persona puede vender y registrar movimientos
              en este turno{canSeeExpected ? '; como dueño, puede cerrarlo si quedó abierto.' : '. Pídale que lo cierre.'}
            </AlertDescription>
          </Alert>
        )}

        <MovementsTable session={session} />

        {session.isMine && <MovementForm />}

        <div className="flex flex-wrap items-end justify-between gap-6">
          {canSeeExpected ? (
            <dl className="grid w-full max-w-xs gap-1 text-sm">
              <div className="flex justify-between">
                <dt className="text-muted-foreground">Efectivo de ventas</dt>
                <dd>{formatMoney(session.netCashFromSales ?? 0)}</dd>
              </div>
              <div className="flex justify-between border-t pt-1 text-base font-semibold">
                <dt>Debe haber en caja</dt>
                <dd>{formatMoney(session.expectedCash ?? 0)}</dd>
              </div>
            </dl>
          ) : (
            <span />
          )}
          {(session.isMine || canSeeExpected) && (
            <CloseShiftDialog session={session} label={session.isMine ? 'Cerrar turno' : 'Cerrar este turno'} />
          )}
        </div>
      </CardContent>
    </Card>
  )
}

function MovementsTable({ session }: { session: CashSession }) {
  if (session.movements.length === 0)
    return <p className="text-sm text-muted-foreground">Sin ingresos ni egresos en este turno.</p>

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Hora</TableHead>
          <TableHead>Tipo</TableHead>
          <TableHead>Motivo</TableHead>
          <TableHead>Registró</TableHead>
          <TableHead className="text-right">Valor</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {session.movements.map((movement) => (
          <TableRow key={movement.id}>
            <TableCell>{formatTime(movement.occurredAt)}</TableCell>
            <TableCell>{movementTypeLabels[movement.type]}</TableCell>
            <TableCell>{movement.reason}</TableCell>
            <TableCell>{movement.registeredByName}</TableCell>
            <TableCell className={cn('text-right', movement.type === 'Expense' && 'text-destructive')}>
              {movement.type === 'Expense' ? '−' : '+'}{formatMoney(movement.amount)}
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
