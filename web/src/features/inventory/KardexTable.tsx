import { useQuery } from '@tanstack/react-query'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDateTime } from '@/lib/format'
import { cn } from '@/lib/utils'
import { getKardex, kardexKey } from './api'
import { formatChange, movementTypeLabels } from './labels'

/** How the product got to today's stock, newest first: who did what, in which batch, and what was left. */
export function KardexTable({ productId, baseUnitName }: { productId: string; baseUnitName: string }) {
  const kardex = useQuery({ queryKey: kardexKey(productId), queryFn: ({ signal }) => getKardex(productId, signal) })

  if (kardex.isPending) return <p className="text-sm text-muted-foreground">Cargando…</p>
  if (kardex.isError) return <p className="text-sm text-destructive">{kardex.error.message}</p>
  if (kardex.data.length === 0) return <p className="text-sm text-muted-foreground">Todavía no hay movimientos.</p>

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Fecha</TableHead>
          <TableHead>Movimiento</TableHead>
          <TableHead>Lote</TableHead>
          <TableHead>Quién</TableHead>
          <TableHead className="text-right">Cambio</TableHead>
          <TableHead className="text-right">Queda ({baseUnitName.toLowerCase()})</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {kardex.data.map((entry) => (
          <TableRow key={entry.id}>
            <TableCell className="whitespace-nowrap">{formatDateTime(entry.occurredAt)}</TableCell>
            <TableCell>
              <div>{entry.document ?? movementTypeLabels[entry.type]}</div>
              {entry.notes && <div className="text-sm text-muted-foreground">{entry.notes}</div>}
            </TableCell>
            <TableCell>{entry.lotNumber}</TableCell>
            <TableCell>{entry.performedByName ?? '—'}</TableCell>
            <TableCell className={cn('text-right font-medium tabular-nums', entry.quantityChange < 0 ? 'text-destructive' : 'text-success')}>
              {formatChange(entry.quantityChange)}
            </TableCell>
            <TableCell className="text-right tabular-nums">{entry.productBalanceAfter}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
