import { useQuery } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { Button, buttonVariants } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { can } from '@/features/auth/permissions'
import { useAuth } from '@/features/auth/useAuth'
import { formatMoney } from '@/lib/format'
import { useDebouncedValue } from '@/lib/useDebouncedValue'
import { cn } from '@/lib/utils'
import { customerKeys, listDebtors, searchCustomers } from './api'
import { CreateCustomerDialog } from './CreateCustomerDialog'
import { describeCredit, documentTypeLabels, oldDebtDays, toneClasses } from './labels'

/** Customers and their credit. Everyone at the register can look; only the owner registers and gives credit. */
export function CustomersPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const isOwner = can(user, 'manageCredit')
  const [view, setView] = useState<'customers' | 'debtors'>('customers')
  const [creating, setCreating] = useState(false)

  return (
    <div className="grid gap-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold">Clientes y fiado</h1>
        {isOwner && (
          <Button onClick={() => setCreating(true)}>
            <Plus />
            Nuevo cliente
          </Button>
        )}
      </div>

      {isOwner && (
        <div className="flex gap-1" role="tablist" aria-label="Vista">
          {(['customers', 'debtors'] as const).map((option) => (
            <Button
              key={option}
              role="tab"
              aria-selected={view === option}
              variant={view === option ? 'secondary' : 'ghost'}
              size="sm"
              onClick={() => setView(option)}
            >
              {option === 'customers' ? 'Clientes' : 'Quién debe'}
            </Button>
          ))}
        </div>
      )}

      {view === 'customers' || !isOwner ? <CustomerList /> : <DebtorList />}

      <CreateCustomerDialog open={creating} onOpenChange={setCreating} onCreated={(id) => void navigate(`/clientes/${id}`)} />
    </div>
  )
}

function CustomerList() {
  const [search, setSearch] = useState('')
  const term = useDebouncedValue(search.trim())
  const customers = useQuery({
    queryKey: customerKeys.search(term),
    queryFn: ({ signal }) => searchCustomers(term, signal),
  })

  return (
    <div className="grid gap-4">
      <Input
        aria-label="Buscar cliente"
        placeholder="Buscar por nombre o número de documento…"
        className="max-w-md"
        autoComplete="off"
        value={search}
        onChange={(event) => setSearch(event.target.value)}
      />
      {customers.isPending && <p className="text-sm text-muted-foreground">Cargando…</p>}
      {customers.isError && <p className="text-sm text-destructive">{customers.error.message}</p>}
      {customers.data?.length === 0 && <p className="text-sm text-muted-foreground">Ningún cliente coincide.</p>}
      {customers.data && customers.data.length > 0 && (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Cliente</TableHead>
              <TableHead>Documento</TableHead>
              <TableHead>Teléfono</TableHead>
              <TableHead>Fiado</TableHead>
              <TableHead className="w-0" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {customers.data.map((customer) => {
              const credit = describeCredit(customer.credit)
              return (
                <TableRow key={customer.id}>
                  <TableCell className="font-medium">{customer.fullName}</TableCell>
                  <TableCell>
                    <span className="text-muted-foreground">{documentTypeLabels[customer.documentType]}</span> {customer.documentNumber}
                  </TableCell>
                  <TableCell>{customer.phone ?? '—'}</TableCell>
                  <TableCell>
                    <Badge className={toneClasses[credit.tone]}>{credit.text}</Badge>
                  </TableCell>
                  <TableCell>
                    <Link to={`/clientes/${customer.id}`} className={buttonVariants({ variant: 'outline', size: 'sm' })}>
                      Ver
                    </Link>
                  </TableCell>
                </TableRow>
              )
            })}
          </TableBody>
        </Table>
      )}
    </div>
  )
}

/** Who owes, the oldest debt first: whom to call. */
function DebtorList() {
  const debtors = useQuery({ queryKey: customerKeys.debtors, queryFn: ({ signal }) => listDebtors(signal) })

  if (debtors.isPending) return <p className="text-sm text-muted-foreground">Cargando…</p>
  if (debtors.isError) return <p className="text-sm text-destructive">{debtors.error.message}</p>
  if (debtors.data.length === 0) return <p className="text-sm text-muted-foreground">Nadie debe: todo el fiado está al día.</p>

  const total = debtors.data.reduce((sum, debtor) => sum + debtor.account.balance, 0)

  return (
    <div className="grid gap-3">
      <p className="text-sm text-muted-foreground">
        {debtors.data.length} {debtors.data.length === 1 ? 'cliente debe' : 'clientes deben'} en total{' '}
        <strong className="text-foreground">{formatMoney(total)}</strong>.
      </p>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Cliente</TableHead>
            <TableHead>Teléfono</TableHead>
            <TableHead className="text-right">Debe</TableHead>
            <TableHead className="text-right">Cupo</TableHead>
            <TableHead className="text-right">Días</TableHead>
            <TableHead className="w-0" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {debtors.data.map((debtor) => {
            const days = debtor.account.daysOwing ?? 0
            return (
              <TableRow key={debtor.customerId}>
                <TableCell className="font-medium">{debtor.customerName}</TableCell>
                <TableCell>{debtor.phone ?? '—'}</TableCell>
                <TableCell className="text-right font-semibold tabular-nums">{formatMoney(debtor.account.balance)}</TableCell>
                <TableCell className="text-right tabular-nums text-muted-foreground">{formatMoney(debtor.account.limit)}</TableCell>
                <TableCell className={cn('text-right tabular-nums', days > oldDebtDays && 'font-semibold text-warning')}>{days}</TableCell>
                <TableCell>
                  <Link to={`/clientes/${debtor.customerId}`} className={buttonVariants({ variant: 'outline', size: 'sm' })}>
                    Ver
                  </Link>
                </TableCell>
              </TableRow>
            )
          })}
        </TableBody>
      </Table>
    </div>
  )
}
