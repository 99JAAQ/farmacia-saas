import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatStock } from '@/features/catalog/stock'
import { formatDate } from '@/lib/format'
import { alertKeys, describeDaysLeft, getStockAlerts, type BatchAlert } from './alerts'

/** What needs attention in the inventory: reorder, write off, sell first. Live: refreshed on every stock change. */
export function AlertsPage() {
  const alerts = useQuery({ queryKey: alertKeys.all, queryFn: ({ signal }) => getStockAlerts(signal) })

  return (
    <div className="grid gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Alertas</h1>
        <p className="text-muted-foreground">Qué pedir, qué dar de baja y qué vender primero. Se actualiza solo.</p>
      </div>
      {alerts.isPending && <p className="text-sm text-muted-foreground">Cargando…</p>}
      {alerts.isError && <p className="text-sm text-destructive">{alerts.error.message}</p>}
      {alerts.data && (
        <>
          <Card>
            <CardHeader>
              <CardTitle>Bajo el mínimo</CardTitle>
              <CardDescription>Hay que pedirlos. El mínimo de cada producto se define en su ficha del catálogo.</CardDescription>
            </CardHeader>
            <CardContent>
              {alerts.data.lowStock.length === 0 ? (
                <p className="text-sm text-muted-foreground">Ningún producto está por debajo de su mínimo.</p>
              ) : (
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Producto</TableHead>
                      <TableHead>Quedan</TableHead>
                      <TableHead>Mínimo</TableHead>
                      <TableHead className="w-0" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {alerts.data.lowStock.map(({ product, sellableUnits, minimumStock }) => (
                      <TableRow key={product.id}>
                        <TableCell className="font-medium">{product.name}</TableCell>
                        <TableCell className={sellableUnits === 0 ? 'font-medium text-destructive' : 'font-medium text-warning'}>
                          {formatStock(sellableUnits, product.saleUnits, product.baseUnitName)}
                        </TableCell>
                        <TableCell className="text-muted-foreground">{formatStock(minimumStock, product.saleUnits, product.baseUnitName)}</TableCell>
                        <TableCell><ProductLink id={product.id} /></TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </CardContent>
          </Card>

          <BatchCard
            title="Vencidos con unidades"
            description="Ya no se pueden vender: hay que darlos de baja (en la ficha del producto, lote por lote)."
            empty="No hay lotes vencidos con unidades."
            batches={alerts.data.expired}
          />
          <BatchCard
            title="Por vencer en 90 días"
            description="Véndalos primero (el POS ya los ofrece primero por FEFO) o devuélvalos al proveedor a tiempo."
            empty="Ningún lote vence en los próximos 90 días."
            batches={alerts.data.expiringSoon}
          />
        </>
      )}
    </div>
  )
}

function BatchCard({ title, description, empty, batches }: { title: string; description: string; empty: string; batches: BatchAlert[] }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent>
        {batches.length === 0 ? (
          <p className="text-sm text-muted-foreground">{empty}</p>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Producto</TableHead>
                <TableHead>Lote</TableHead>
                <TableHead>Vence</TableHead>
                <TableHead>Unidades</TableHead>
                <TableHead className="w-0" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {batches.map((batch) => (
                <TableRow key={batch.batchId}>
                  <TableCell className="font-medium">{batch.product.name}</TableCell>
                  <TableCell>{batch.lotNumber}</TableCell>
                  <TableCell>
                    <span className="flex flex-wrap items-center gap-2">
                      {formatDate(batch.expirationDate)}
                      <Badge className={batch.daysLeft < 0 ? 'bg-destructive/10 text-destructive' : 'bg-warning/10 text-warning'}>
                        {describeDaysLeft(batch.daysLeft)}
                      </Badge>
                    </span>
                  </TableCell>
                  <TableCell>{formatStock(batch.quantity, batch.product.saleUnits, batch.product.baseUnitName)}</TableCell>
                  <TableCell><ProductLink id={batch.product.id} /></TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  )
}

function ProductLink({ id }: { id: string }) {
  return (
    <Link to={`/catalogo/${id}`} className={buttonVariants({ variant: 'outline', size: 'sm' })}>
      Ver
    </Link>
  )
}
