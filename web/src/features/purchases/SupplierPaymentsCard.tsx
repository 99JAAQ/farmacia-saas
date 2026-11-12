import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { can } from '@/features/auth/permissions'
import { useAuth } from '@/features/auth/useAuth'
import { formatDateTime, formatMoney } from '@/lib/format'
import { purchaseKeys, registerSupplierPayment, type Purchase, type SupplierPaymentMethod } from './api'
import { paymentProblem, supplierPaymentMethodLabels } from './labels'

const methods = Object.keys(supplierPaymentMethodLabels) as SupplierPaymentMethod[]

/** A confirmed credit invoice: what was paid, what is owed, and (owner only) "Registrar pago". */
export function SupplierPaymentsCard({ purchase }: { purchase: Purchase }) {
  const { user } = useAuth()
  const [paying, setPaying] = useState(false)

  return (
    <Card>
      <CardHeader>
        <div className="flex flex-wrap items-center gap-3">
          <CardTitle>Pagos al proveedor</CardTitle>
          {purchase.balance === 0 ? (
            <Badge className="bg-success/10 text-success">Pagada</Badge>
          ) : (
            purchase.isOverdue && <Badge className="bg-destructive/10 text-destructive">Vencida</Badge>
          )}
        </div>
        <CardDescription>Se paga fuera de la caja: por transferencia, de la caja fuerte o con cheque.</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-5">
        <dl className="grid gap-4 sm:grid-cols-3">
          <Figure label="Total de la factura" value={formatMoney(purchase.totals.total)} />
          <Figure label="Pagado" value={formatMoney(purchase.amountPaid)} />
          <Figure label="Saldo" value={formatMoney(purchase.balance)} strong />
        </dl>

        {purchase.payments.length > 0 && (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Fecha</TableHead>
                <TableHead>Medio</TableHead>
                <TableHead>Referencia</TableHead>
                <TableHead>Registró</TableHead>
                <TableHead className="text-right">Valor</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {purchase.payments.map((payment) => (
                <TableRow key={payment.id}>
                  <TableCell>{formatDateTime(payment.paidAt)}</TableCell>
                  <TableCell>{supplierPaymentMethodLabels[payment.method]}</TableCell>
                  <TableCell>{payment.reference ?? '—'}</TableCell>
                  <TableCell>{payment.paidByName}</TableCell>
                  <TableCell className="text-right tabular-nums">{formatMoney(payment.amount)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}

        {purchase.balance > 0 &&
          (can(user, 'paySuppliers') ? (
            <div>
              <Button onClick={() => setPaying(true)}>Registrar pago</Button>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">Los pagos a proveedores los registra el dueño.</p>
          ))}

        {/* Mounted only while open: each opening starts empty. */}
        {paying && <PaymentDialog purchase={purchase} onClose={() => setPaying(false)} />}
      </CardContent>
    </Card>
  )
}

function Figure({ label, value, strong = false }: { label: string; value: string; strong?: boolean }) {
  return (
    <div className="grid gap-0.5">
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className={strong ? 'text-2xl font-semibold tabular-nums' : 'text-lg tabular-nums'}>{value}</dd>
    </div>
  )
}

function PaymentDialog({ purchase, onClose }: { purchase: Purchase; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [amountText, setAmountText] = useState('')
  const [method, setMethod] = useState<SupplierPaymentMethod>('BankTransfer')
  const [reference, setReference] = useState('')
  const problem = paymentProblem(amountText, purchase.balance, method, reference)

  const mutation = useMutation({
    // Cash from the safe carries no number, even if one was typed before switching to it.
    mutationFn: () =>
      registerSupplierPayment(purchase.id, Number(amountText), method, method === 'Cash' ? null : reference.trim() || null),
    // The invoice's balance and the payables list both changed.
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: purchaseKeys.all })
      onClose()
    },
  })

  const edit = (setter: (value: string) => void) => (value: string) => {
    mutation.reset()
    setter(value)
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Pagar la factura {purchase.invoiceNumber}</DialogTitle>
          <DialogDescription>
            {purchase.supplierName} · saldo {formatMoney(purchase.balance)}. Puede pagar una parte o todo.
          </DialogDescription>
        </DialogHeader>
        <form
          id="supplier-payment"
          className="grid gap-4"
          onSubmit={(event) => {
            event.preventDefault()
            if (problem === null) mutation.mutate()
          }}
        >
          {mutation.isError && (
            <Alert variant="destructive" role="alert">
              <AlertDescription>{mutation.error.message}</AlertDescription>
            </Alert>
          )}
          <div className="grid gap-2">
            <Label htmlFor="payment-amount">Valor</Label>
            <div className="flex gap-2">
              <Input
                id="payment-amount"
                type="number"
                min={0}
                step="any"
                inputMode="decimal"
                autoFocus
                className="tabular-nums"
                value={amountText}
                onChange={(event) => edit(setAmountText)(event.target.value)}
              />
              <Button type="button" variant="outline" onClick={() => edit(setAmountText)(String(purchase.balance))}>
                Todo
              </Button>
            </div>
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="grid gap-2">
              <Label htmlFor="payment-method">Medio</Label>
              <NativeSelect
                id="payment-method"
                value={method}
                onChange={(event) => edit((value) => setMethod(value as SupplierPaymentMethod))(event.target.value)}
              >
                {methods.map((option) => (
                  <NativeSelectOption key={option} value={option}>
                    {supplierPaymentMethodLabels[option]}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
            </div>
            <div className="grid gap-2">
              <Label htmlFor="payment-reference">{method === 'Check' ? 'Número del cheque' : 'Número de la transferencia'}</Label>
              <Input
                id="payment-reference"
                autoComplete="off"
                maxLength={100}
                disabled={method === 'Cash'}
                placeholder={method === 'Cash' ? 'No aplica' : undefined}
                value={method === 'Cash' ? '' : reference}
                onChange={(event) => edit(setReference)(event.target.value)}
              />
            </div>
          </div>
          {problem !== null && amountText.trim() !== '' && <p className="text-sm text-muted-foreground">{problem}</p>}
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit" form="supplier-payment" disabled={problem !== null || mutation.isPending}>
            {mutation.isPending ? 'Registrando…' : problem === null ? `Pagar ${formatMoney(Number(amountText))}` : 'Pagar'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
