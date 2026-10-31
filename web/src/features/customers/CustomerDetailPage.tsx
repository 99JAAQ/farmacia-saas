import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft } from 'lucide-react'
import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button, buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { can } from '@/features/auth/permissions'
import { useAuth } from '@/features/auth/useAuth'
import { formatDateTime, formatMoney } from '@/lib/format'
import { cn } from '@/lib/utils'
import {
  changeCreditLimit,
  customerKeys,
  getCreditStatement,
  getCustomer,
  openCredit,
  setCreditSuspended,
  type Customer,
} from './api'
import { creditEntryLabels, describeCredit, documentTypeLabels, toneClasses } from './labels'
import { ReceivePayment } from './ReceivePayment'

export function CustomerDetailPage() {
  const { id = '' } = useParams()
  const customer = useQuery({ queryKey: customerKeys.detail(id), queryFn: ({ signal }) => getCustomer(id, signal) })

  return (
    <div className="grid gap-6">
      <Link to="/clientes" className={cn(buttonVariants({ variant: 'ghost', size: 'sm' }), 'w-fit')}>
        <ArrowLeft />
        Clientes
      </Link>
      {customer.isPending && <p className="text-sm text-muted-foreground">Cargando…</p>}
      {customer.isError && <p className="text-sm text-destructive">{customer.error.message}</p>}
      {customer.data && <CustomerView customer={customer.data} />}
    </div>
  )
}

function CustomerView({ customer }: { customer: Customer }) {
  const credit = describeCredit(customer.credit)

  return (
    <>
      <div className="grid gap-1">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-2xl font-semibold">{customer.fullName}</h1>
          <Badge className={toneClasses[credit.tone]}>{credit.text}</Badge>
        </div>
        <p className="text-muted-foreground">
          {documentTypeLabels[customer.documentType]} {customer.documentNumber}
          {customer.phone && ` · ${customer.phone}`}
          {customer.email && ` · ${customer.email}`}
        </p>
      </div>
      <CreditPanel customer={customer} />
      {customer.credit && <Statement customerId={customer.id} />}
    </>
  )
}

/** The account's numbers, and the owner's controls: enable credit, change the limit, suspend or resume. */
function CreditPanel({ customer }: { customer: Customer }) {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const isOwner = can(user, 'manageCredit')
  const account = customer.credit
  const [limitText, setLimitText] = useState(account ? String(account.limit) : '')
  const limit = Number(limitText)
  const validLimit = limitText.trim() !== '' && Number.isFinite(limit) && limit > 0

  // Every change refreshes the customer, its statement, the search and the debtors (they all start with 'customers').
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: customerKeys.all })
  const saveLimit = useMutation({
    mutationFn: () => (account ? changeCreditLimit(customer.id, limit) : openCredit(customer.id, limit)),
    onSuccess,
  })
  const toggle = useMutation({ mutationFn: () => setCreditSuspended(customer.id, !account?.isSuspended), onSuccess })
  const error = saveLimit.error ?? toggle.error

  return (
    <Card>
      <CardHeader>
        <CardTitle>Fiado</CardTitle>
        {!account && (
          <CardDescription>
            {isOwner
              ? 'Este cliente todavía no tiene fiado. Habilítelo con el cupo máximo que le autoriza.'
              : 'Este cliente no tiene fiado habilitado. Si debe tenerlo, pídaselo al dueño.'}
          </CardDescription>
        )}
      </CardHeader>
      <CardContent className="grid gap-5">
        {account && (
          <dl className="grid gap-4 sm:grid-cols-4">
            <Figure label="Debe" value={formatMoney(account.balance)} strong />
            <Figure label="Cupo" value={formatMoney(account.limit)} />
            <Figure label="Disponible" value={formatMoney(account.available)} />
            <Figure
              label="Debe desde"
              value={account.debtSince ? `${formatDateTime(account.debtSince)} (${account.daysOwing} d)` : '—'}
            />
          </dl>
        )}

        {account && <ReceivePayment customer={customer} />}

        {error && (
          <Alert variant="destructive" role="alert">
            <AlertDescription>{error.message}</AlertDescription>
          </Alert>
        )}

        {isOwner && (
          <div className="flex flex-wrap items-end gap-3">
            <form
              className="flex items-end gap-2"
              onSubmit={(event) => {
                event.preventDefault()
                if (validLimit) saveLimit.mutate()
              }}
            >
              <div className="grid gap-2">
                <Label htmlFor="credit-limit">{account ? 'Cupo' : 'Cupo autorizado'}</Label>
                <Input
                  id="credit-limit"
                  type="number"
                  min={1}
                  step="any"
                  inputMode="decimal"
                  className="w-40 tabular-nums"
                  value={limitText}
                  onChange={(event) => {
                    saveLimit.reset()
                    setLimitText(event.target.value)
                  }}
                />
              </div>
              <Button type="submit" disabled={!validLimit || saveLimit.isPending || (account?.limit === limit)}>
                {account ? 'Cambiar cupo' : 'Habilitar fiado'}
              </Button>
            </form>
            {account && (
              <Button variant="outline" disabled={toggle.isPending} onClick={() => toggle.mutate()}>
                {account.isSuspended ? 'Reanudar fiado' : 'Suspender fiado'}
              </Button>
            )}
          </div>
        )}
        {isOwner && account && account.limit < account.balance && (
          <p className="text-sm text-muted-foreground">
            El cupo está por debajo de lo que debe: no se le puede fiar más hasta que abone.
          </p>
        )}
      </CardContent>
    </Card>
  )
}

function Figure({ label, value, strong = false }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className="grid gap-0.5">
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className={cn('tabular-nums', strong ? 'text-2xl font-semibold' : 'text-lg')}>{value}</dd>
    </div>
  )
}

/** How the balance got there, newest first: what the cashier reads to the customer. */
function Statement({ customerId }: { customerId: string }) {
  const statement = useQuery({
    queryKey: customerKeys.statement(customerId),
    queryFn: ({ signal }) => getCreditStatement(customerId, signal),
  })

  return (
    <section aria-labelledby="statement" className="grid gap-3">
      <h2 id="statement" className="text-lg font-semibold">
        Movimientos
      </h2>
      {statement.isPending && <p className="text-sm text-muted-foreground">Cargando…</p>}
      {statement.isError && <p className="text-sm text-destructive">{statement.error.message}</p>}
      {statement.data?.entries.length === 0 && <p className="text-sm text-muted-foreground">Todavía no hay movimientos.</p>}
      {statement.data && statement.data.entries.length > 0 && (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Fecha</TableHead>
              <TableHead>Movimiento</TableHead>
              <TableHead className="text-right">Valor</TableHead>
              <TableHead className="text-right">Saldo</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {statement.data.entries.map((entry) => (
              <TableRow key={`${entry.type}:${entry.referenceId}`}>
                <TableCell>{formatDateTime(entry.occurredAt)}</TableCell>
                <TableCell>{creditEntryLabels[entry.type]}</TableCell>
                <TableCell className={cn('text-right tabular-nums', entry.change < 0 && 'text-success')}>
                  {entry.change > 0 ? '+' : '−'}
                  {formatMoney(Math.abs(entry.change))}
                </TableCell>
                <TableCell className="text-right font-medium tabular-nums">{formatMoney(entry.balanceAfter)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </section>
  )
}
