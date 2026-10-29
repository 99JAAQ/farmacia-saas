import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatMoney, formatTime } from '@/lib/format'
import { getSale, listShiftSales, saleKeys } from './api'
import { paymentMethodLabels } from './labels'
import { TicketDialog } from './Ticket'

/** The shift's sales, newest first, to print a ticket again. */
export function ShiftSales({ cashSessionId }: { cashSessionId: string }) {
  const [selectedId, setSelectedId] = useState<string | null>(null)

  const sales = useQuery({
    queryKey: saleKeys.byShift(cashSessionId),
    queryFn: ({ signal }) => listShiftSales(cashSessionId, signal),
  })
  const selected = useQuery({
    queryKey: saleKeys.detail(selectedId ?? ''),
    queryFn: ({ signal }) => getSale(selectedId ?? '', signal),
    enabled: selectedId !== null,
  })

  if (sales.isPending) return <p className="text-sm text-muted-foreground">Cargando las ventas del turno…</p>
  if (sales.isError) return <p className="text-sm text-destructive">{sales.error.message}</p>
  if (sales.data.length === 0) return <p className="text-sm text-muted-foreground">Todavía no hay ventas en este turno.</p>

  return (
    <>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>N.º</TableHead>
            <TableHead>Hora</TableHead>
            <TableHead>Pago</TableHead>
            <TableHead className="text-right">Total</TableHead>
            <TableHead className="w-0" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {sales.data.map((sale) => (
            <TableRow key={sale.id}>
              <TableCell className="font-medium">{sale.number}</TableCell>
              <TableCell>{formatTime(sale.soldAt)}</TableCell>
              <TableCell>{sale.methods.map((method) => paymentMethodLabels[method]).join(' + ')}</TableCell>
              <TableCell className="text-right tabular-nums">{formatMoney(sale.total)}</TableCell>
              <TableCell>
                <Button variant="outline" size="sm" onClick={() => setSelectedId(sale.id)}>
                  Ver tiquete
                </Button>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      {selected.isError && <p className="text-sm text-destructive">{selected.error.message}</p>}
      <TicketDialog
        sale={selectedId !== null && selected.data?.id === selectedId ? selected.data : null}
        title="Tiquete"
        closeLabel="Cerrar"
        onClose={() => setSelectedId(null)}
      />
    </>
  )
}
