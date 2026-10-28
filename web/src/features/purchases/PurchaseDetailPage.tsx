import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useParams } from 'react-router'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button, buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { formatDate, formatMoney } from '@/lib/format'
import { AddItemForm } from './AddItemForm'
import { getPurchase, purchaseKeys, removePurchaseItem, type Purchase } from './api'
import { ConfirmPurchaseDialog } from './ConfirmPurchaseDialog'
import { InvoiceDiscountForm } from './InvoiceDiscountForm'
import { paymentTermLabels, statusLabels, vatRateLabels } from './labels'

export function PurchaseDetailPage() {
  const { id = '' } = useParams()
  const purchase = useQuery({
    queryKey: purchaseKeys.detail(id),
    queryFn: ({ signal }) => getPurchase(id, signal),
  })

  if (purchase.isPending) return <p className="text-sm text-muted-foreground">Cargando…</p>
  if (purchase.isError) return <p className="text-sm text-destructive">{purchase.error.message}</p>

  return <PurchaseView purchase={purchase.data} />
}

function PurchaseView({ purchase }: { purchase: Purchase }) {
  const isDraft = purchase.status === 'Draft'

  return (
    <div className="grid gap-6">
      <div className="flex flex-wrap items-center gap-3">
        <Link to="/compras" className={buttonVariants({ variant: 'ghost', size: 'sm' })}>
          ← Compras
        </Link>
        <h1 className="text-2xl font-semibold">Factura {purchase.invoiceNumber}</h1>
        <Badge variant={isDraft ? 'secondary' : 'default'}>{statusLabels[purchase.status]}</Badge>
      </div>

      {!isDraft && purchase.confirmedAt && (
        <Alert>
          <AlertDescription>
            Mercancía recibida el {new Date(purchase.confirmedAt).toLocaleString('es-CO')}: {purchase.items.length}{' '}
            {purchase.items.length === 1 ? 'lote entró' : 'lotes entraron'} al inventario con su costo real por unidad.
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader>
          <CardTitle>{purchase.supplierName}</CardTitle>
          <CardDescription>
            Factura del {formatDate(purchase.invoiceDate)} · {paymentTermLabels[purchase.paymentTerm]}
            {purchase.dueDate && ` · paga el ${formatDate(purchase.dueDate)}`}
          </CardDescription>
        </CardHeader>
        <CardContent className="grid gap-6">
          <ItemsTable purchase={purchase} />

          {isDraft && <AddItemForm purchaseId={purchase.id} />}

          <div className="flex flex-wrap items-start justify-between gap-6">
            {isDraft ? (
              // Remounted when the server value changes, so the field shows the saved discount.
              <InvoiceDiscountForm key={purchase.invoiceDiscount} purchaseId={purchase.id} current={purchase.invoiceDiscount} />
            ) : (
              <span />
            )}
            <Totals purchase={purchase} />
          </div>

          {isDraft && (
            <div className="flex justify-end">
              <ConfirmPurchaseDialog purchaseId={purchase.id} disabled={purchase.items.length === 0} />
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  )
}

function ItemsTable({ purchase }: { purchase: Purchase }) {
  const queryClient = useQueryClient()
  const isDraft = purchase.status === 'Draft'

  const remove = useMutation({
    mutationFn: (itemId: string) => removePurchaseItem(purchase.id, itemId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: purchaseKeys.detail(purchase.id) }),
  })

  if (purchase.items.length === 0)
    return <p className="text-sm text-muted-foreground">Todavía no tiene líneas.</p>

  return (
    <div className="grid gap-2">
      {remove.isError && <p className="text-sm text-destructive">{remove.error.message}</p>}
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Producto</TableHead>
            <TableHead>Lote</TableHead>
            <TableHead>Vence</TableHead>
            <TableHead className="text-right">Cant. + bonif.</TableHead>
            <TableHead className="text-right">Costo</TableHead>
            <TableHead className="text-right">IVA</TableHead>
            <TableHead className="text-right">Neto</TableHead>
            {isDraft ? <TableHead /> : <TableHead className="text-right">Costo por unidad</TableHead>}
          </TableRow>
        </TableHeader>
        <TableBody>
          {purchase.items.map((item) => (
            <TableRow key={item.id}>
              <TableCell>
                <div className="font-medium">{item.productName}</div>
                <div className="text-xs text-muted-foreground">{item.saleUnitName} · {item.baseUnits} unidades</div>
              </TableCell>
              <TableCell>{item.lotNumber}</TableCell>
              <TableCell>{formatDate(item.expirationDate)}</TableCell>
              <TableCell className="text-right">
                {item.quantity}
                {item.bonusQuantity > 0 && ` + ${item.bonusQuantity}`}
              </TableCell>
              <TableCell className="text-right">{formatMoney(item.unitCost)}</TableCell>
              <TableCell className="text-right">{vatRateLabels[item.vatRate]}</TableCell>
              <TableCell className="text-right">{formatMoney(item.netAmount)}</TableCell>
              {isDraft ? (
                <TableCell className="text-right">
                  <Button variant="ghost" size="sm" disabled={remove.isPending} onClick={() => remove.mutate(item.id)}
                    aria-label={`Quitar ${item.productName}, lote ${item.lotNumber}`}>
                    Quitar
                  </Button>
                </TableCell>
              ) : (
                <TableCell className="text-right">
                  {item.unitCostPerBaseUnit !== null && formatMoney(item.unitCostPerBaseUnit)}
                </TableCell>
              )}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}

/** Always the totals calculated by the server: a single source of truth for money. */
function Totals({ purchase }: { purchase: Purchase }) {
  const { totals } = purchase
  const rows: [string, number][] = [
    ['Subtotal', totals.gross],
    ['Descuentos de línea', -totals.lineDiscounts],
    ['Descuento de factura', -totals.invoiceDiscount],
    ['IVA', totals.vat],
  ]

  return (
    <dl className="grid w-full max-w-xs gap-1 text-sm">
      {rows.map(([label, amount]) => (
        <div key={label} className="flex justify-between">
          <dt className="text-muted-foreground">{label}</dt>
          <dd>{formatMoney(amount)}</dd>
        </div>
      ))}
      <div className="flex justify-between border-t pt-1 text-base font-semibold">
        <dt>Total</dt>
        <dd>{formatMoney(totals.total)}</dd>
      </div>
    </dl>
  )
}
