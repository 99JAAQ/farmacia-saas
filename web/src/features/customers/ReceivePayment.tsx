import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Printer } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { cashKeys, getCurrentCashSession } from '@/features/cash/api'
import { paymentMethodLabels } from '@/features/sales/labels'
import { formatMoney } from '@/lib/format'
import { customerKeys, registerCreditPayment, type CreditPayment, type CreditPaymentMethod, type Customer } from './api'
import { CreditPaymentVoucher } from './CreditPaymentVoucher'

const methods: CreditPaymentMethod[] = ['Cash', 'Card', 'DigitalWallet', 'BankTransfer']

/**
 * "Recibir abono". In cash it goes into the drawer of whoever receives it, so it needs a shift of one's own: without
 * one, this says so (with a link to the register) instead of letting the API answer 403 or 409.
 */
export function ReceivePayment({ customer }: { customer: Customer }) {
  const current = useQuery({ queryKey: cashKeys.current, queryFn: ({ signal }) => getCurrentCashSession(signal) })
  const [open, setOpen] = useState(false)
  const balance = customer.credit?.balance ?? 0

  if (balance === 0) return null
  if (current.data && !current.data.session?.isMine)
    return (
      <p className="text-sm text-muted-foreground">
        Para recibir abonos abra su turno en <Link to="/caja" className="underline">Caja</Link>.
      </p>
    )

  return (
    <>
      <Button disabled={!current.data} onClick={() => setOpen(true)}>
        Recibir abono
      </Button>
      {/* Mounted only while open: closing unmounts it, so the next one starts empty, without the previous voucher. */}
      {open && <PaymentDialog customer={customer} balance={balance} onClose={() => setOpen(false)} />}
    </>
  )
}

function PaymentDialog({ customer, balance, onClose }: { customer: Customer; balance: number; onClose: () => void }) {
  const queryClient = useQueryClient()
  const [amountText, setAmountText] = useState('')
  const [method, setMethod] = useState<CreditPaymentMethod>('Cash')
  const amount = Number(amountText)
  const problem =
    amountText.trim() === '' || !Number.isFinite(amount) || amount <= 0
      ? 'Escriba el valor del abono.'
      : amount > balance
        ? `El abono no puede superar la deuda (${formatMoney(balance)}).`
        : null

  const mutation = useMutation({
    mutationFn: () => registerCreditPayment(customer.id, method, amount),
    // The balance, the statement and the debtors changed.
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: customerKeys.all }),
  })
  const voucher: CreditPayment | undefined = mutation.data

  return (
    <Dialog open onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="sm:max-w-sm">
        {voucher ? (
          <>
            <DialogHeader>
              <DialogTitle>Abono registrado</DialogTitle>
            </DialogHeader>
            <CreditPaymentVoucher payment={voucher} />
            <DialogFooter>
              <Button variant="outline" onClick={() => window.print()}>
                <Printer />
                Imprimir
              </Button>
              <Button autoFocus onClick={onClose}>
                Listo
              </Button>
            </DialogFooter>
          </>
        ) : (
          <>
            <DialogHeader>
              <DialogTitle>Abono de {customer.fullName}</DialogTitle>
              <DialogDescription>Debe {formatMoney(balance)}. El abono va a su saldo, no a un tiquete.</DialogDescription>
            </DialogHeader>
            <form
              id="credit-payment"
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
                    className="h-11 text-lg tabular-nums"
                    value={amountText}
                    onChange={(event) => {
                      mutation.reset()
                      setAmountText(event.target.value)
                    }}
                  />
                  <Button type="button" variant="outline" className="h-11" onClick={() => setAmountText(String(balance))}>
                    Todo
                  </Button>
                </div>
              </div>
              <div className="grid gap-2">
                <Label htmlFor="payment-method">Medio de pago</Label>
                <NativeSelect id="payment-method" value={method} onChange={(event) => setMethod(event.target.value as CreditPaymentMethod)}>
                  {methods.map((option) => (
                    <NativeSelectOption key={option} value={option}>
                      {paymentMethodLabels[option]}
                    </NativeSelectOption>
                  ))}
                </NativeSelect>
              </div>
              {problem !== null && amountText.trim() !== '' && <p className="text-sm text-destructive">{problem}</p>}
            </form>
            <DialogFooter>
              <Button type="submit" form="credit-payment" disabled={problem !== null || mutation.isPending}>
                {mutation.isPending ? 'Registrando…' : problem === null ? `Recibir ${formatMoney(amount)}` : 'Recibir abono'}
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  )
}
