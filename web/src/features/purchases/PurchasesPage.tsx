import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDate, formatMoney } from '@/lib/format'
import { listPurchases, purchaseKeys, type PurchaseStatus, type PurchaseSummary } from './api'
import { statusLabels } from './labels'

export function PurchasesPage() {
  const drafts = useQuery({
    queryKey: purchaseKeys.list('Draft'),
    queryFn: ({ signal }) => listPurchases('Draft', signal),
  })
  const recent = useQuery({
    queryKey: purchaseKeys.list('Confirmed'),
    queryFn: ({ signal }) => listPurchases('Confirmed', signal),
  })

  return (
    <div className="grid gap-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Compras</h1>
        <Link to="/compras/nueva" className={buttonVariants()}>
          Nueva compra
        </Link>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Borradores</CardTitle>
          <CardDescription>Facturas a medio digitar: se pueden retomar después de un corte de luz.</CardDescription>
        </CardHeader>
        <CardContent>
          <PurchaseTable query={drafts} empty="No hay borradores pendientes." action="Continuar" />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Recibidas recientemente</CardTitle>
        </CardHeader>
        <CardContent>
          <PurchaseTable query={recent} empty="Todavía no hay compras confirmadas." action="Ver" />
        </CardContent>
      </Card>
    </div>
  )
}

const statusVariant: Record<PurchaseStatus, 'secondary' | 'default'> = { Draft: 'secondary', Confirmed: 'default' }

function PurchaseTable({
  query,
  empty,
  action,
}: {
  query: ReturnType<typeof useQuery<PurchaseSummary[]>>
  empty: string
  action: string
}) {
  if (query.isPending) return <p className="text-sm text-muted-foreground">Cargando…</p>
  if (query.isError) return <p className="text-sm text-destructive">{query.error.message}</p>
  if (query.data.length === 0) return <p className="text-sm text-muted-foreground">{empty}</p>

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Factura</TableHead>
          <TableHead>Proveedor</TableHead>
          <TableHead>Fecha</TableHead>
          <TableHead className="text-right">Líneas</TableHead>
          <TableHead className="text-right">Total</TableHead>
          <TableHead>Estado</TableHead>
          <TableHead />
        </TableRow>
      </TableHeader>
      <TableBody>
        {query.data.map((purchase) => (
          <TableRow key={purchase.id}>
            <TableCell className="font-medium">{purchase.invoiceNumber}</TableCell>
            <TableCell>{purchase.supplierName}</TableCell>
            <TableCell>{formatDate(purchase.invoiceDate)}</TableCell>
            <TableCell className="text-right">{purchase.itemCount}</TableCell>
            <TableCell className="text-right">{formatMoney(purchase.total)}</TableCell>
            <TableCell>
              <Badge variant={statusVariant[purchase.status]}>{statusLabels[purchase.status]}</Badge>
            </TableCell>
            <TableCell className="text-right">
              <Link to={`/compras/${purchase.id}`} className={buttonVariants({ variant: 'outline', size: 'sm' })}>
                {action}
              </Link>
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
