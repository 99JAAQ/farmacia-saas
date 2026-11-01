import { useQuery } from '@tanstack/react-query'
import { Badge } from '@/components/ui/badge'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { can } from '@/features/auth/permissions'
import { useAuth } from '@/features/auth/useAuth'
import { BatchActions } from '@/features/inventory/BatchActions'
import { getProductBatches, productKeys, type Product } from '@/features/products/api'
import { formatDate, formatMoney, todayLocal } from '@/lib/format'
import { cn } from '@/lib/utils'
import { daysUntil, expiringSoonDays, formatStock } from './stock'

/** The product's batches with units, the first to expire first (the one FEFO sells first), with their cost. */
export function BatchesTable({ product }: { product: Product }) {
  const batches = useQuery({ queryKey: productKeys.batches(product.id), queryFn: ({ signal }) => getProductBatches(product.id, signal) })
  const today = todayLocal()
  // Write-offs and counts are the owner's and the stock keeper's; the API enforces it (ManageInventory).
  const { user } = useAuth()
  const managesInventory = can(user, 'manageInventory')

  if (batches.isPending) return <p className="text-sm text-muted-foreground">Cargando…</p>
  if (batches.isError) return <p className="text-sm text-destructive">{batches.error.message}</p>
  if (batches.data.length === 0) return <p className="text-sm text-muted-foreground">No hay lotes con unidades. Entran al confirmar una compra.</p>

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Lote</TableHead>
          <TableHead>Vence</TableHead>
          <TableHead>Unidades</TableHead>
          <TableHead className="text-right">Costo por {product.baseUnitName.toLowerCase()}</TableHead>
          <TableHead>Recibido</TableHead>
          {managesInventory && <TableHead className="w-0" />}
        </TableRow>
      </TableHeader>
      <TableBody>
        {batches.data.map((batch) => {
          const days = daysUntil(batch.expirationDate, today)
          return (
            <TableRow key={batch.id}>
              <TableCell className="font-medium">{batch.lotNumber}</TableCell>
              <TableCell>
                <span className="flex flex-wrap items-center gap-2">
                  {formatDate(batch.expirationDate)}
                  {batch.isExpired ? (
                    <Badge className="bg-destructive/10 text-destructive">Vencido</Badge>
                  ) : (
                    days <= expiringSoonDays && (
                      <Badge className="bg-warning/10 text-warning">{days === 0 ? 'Vence hoy' : `En ${days} días`}</Badge>
                    )
                  )}
                </span>
              </TableCell>
              <TableCell className={cn(batch.isExpired && 'text-muted-foreground line-through')}>
                {formatStock(batch.quantity, product.saleUnits, product.baseUnitName)}
              </TableCell>
              <TableCell className="text-right tabular-nums">{formatMoney(batch.unitCost)}</TableCell>
              {/* The API sends times with Colombia's offset (-05:00), so the first 10 characters are the local date. */}
              <TableCell>{formatDate(batch.receivedAt.slice(0, 10))}</TableCell>
              {managesInventory && (
                <TableCell>
                  <BatchActions batch={batch} baseUnitName={product.baseUnitName} />
                </TableCell>
              )}
            </TableRow>
          )
        })}
      </TableBody>
    </Table>
  )
}
