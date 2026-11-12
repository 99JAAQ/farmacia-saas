import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDate, formatMoney } from '@/lib/format'
import { listPayables, purchaseKeys } from './api'
import { describeDue } from './labels'

const toneClasses = {
  muted: 'bg-muted text-muted-foreground',
  warning: 'bg-warning/10 text-warning',
  destructive: 'bg-destructive/10 text-destructive',
} as const

/** What is owed to suppliers, the most overdue first: the order to pay in. */
export function PayablesCard() {
  const payables = useQuery({ queryKey: purchaseKeys.payables, queryFn: ({ signal }) => listPayables(signal) })
  const total = payables.data?.reduce((sum, invoice) => sum + invoice.balance, 0) ?? 0
  const overdue = payables.data?.filter((invoice) => invoice.isOverdue) ?? []

  return (
    <Card>
      <CardHeader>
        <CardTitle>Por pagar</CardTitle>
        <CardDescription>
          {payables.data && payables.data.length > 0 ? (
            <>
              Se deben <strong className="text-foreground">{formatMoney(total)}</strong> en {payables.data.length}{' '}
              {payables.data.length === 1 ? 'factura' : 'facturas'} a crédito
              {overdue.length > 0 && (
                <>
                  , <strong className="text-destructive">{overdue.length} vencida{overdue.length === 1 ? '' : 's'}</strong>
                </>
              )}
              .
            </>
          ) : (
            'Facturas compradas a crédito que todavía se deben.'
          )}
        </CardDescription>
      </CardHeader>
      <CardContent>
        {payables.isPending && <p className="text-sm text-muted-foreground">Cargando…</p>}
        {payables.isError && <p className="text-sm text-destructive">{payables.error.message}</p>}
        {payables.data?.length === 0 && <p className="text-sm text-muted-foreground">No se le debe nada a ningún proveedor.</p>}
        {payables.data && payables.data.length > 0 && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Proveedor</TableHead>
                <TableHead>Factura</TableHead>
                <TableHead>Vencimiento</TableHead>
                <TableHead className="text-right">Total</TableHead>
                <TableHead className="text-right">Saldo</TableHead>
                <TableHead className="w-0" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {payables.data.map((invoice) => {
                const due = describeDue(invoice.daysToDue)
                return (
                  <TableRow key={invoice.purchaseId}>
                    <TableCell className="font-medium">{invoice.supplierName}</TableCell>
                    <TableCell>{invoice.invoiceNumber}</TableCell>
                    <TableCell>
                      <span className="flex flex-wrap items-center gap-2">
                        {invoice.dueDate ? formatDate(invoice.dueDate) : '—'}
                        <Badge className={toneClasses[due.tone]}>{due.text}</Badge>
                      </span>
                    </TableCell>
                    <TableCell className="text-right tabular-nums text-muted-foreground">{formatMoney(invoice.total)}</TableCell>
                    <TableCell className="text-right font-semibold tabular-nums">{formatMoney(invoice.balance)}</TableCell>
                    <TableCell>
                      <Link to={`/compras/${invoice.purchaseId}`} className={buttonVariants({ variant: 'outline', size: 'sm' })}>
                        Ver
                      </Link>
                    </TableCell>
                  </TableRow>
                )
              })}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  )
}
