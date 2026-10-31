import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Minus, Plus } from 'lucide-react'
import { useRef, useState } from 'react'
import { PrintDialog } from '@/components/PrintDialog'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { RequireOwnShift } from '@/features/cash/RequireOwnShift'
import { getSaleByNumber, saleKeys, type Sale } from '@/features/sales/api'
import { formatDateTime, formatMoney } from '@/lib/format'
import { cn } from '@/lib/utils'
import { registerSaleReturn, type ReturnDisposition, type SaleReturn } from './api'
import { dispositionLabels } from './labels'
import { draftFrom, refundOf, returnProblem, setDisposition, setReturnQuantity, toRequest } from './returnDraft'
import { ReturnVoucher } from './ReturnVoucher'

/** Refunds come out of the drawer, so returns need a shift of one's own, like selling. */
export function ReturnsPage() {
  return <RequireOwnShift action="hacer devoluciones">{() => <Returns />}</RequireOwnShift>
}

function Returns() {
  const numberRef = useRef<HTMLInputElement>(null)
  const [numberText, setNumberText] = useState('')
  const [searched, setSearched] = useState<number | null>(null)
  const [voucher, setVoucher] = useState<SaleReturn | null>(null)

  const sale = useQuery({
    queryKey: saleKeys.byNumber(searched ?? 0),
    queryFn: ({ signal }) => getSaleByNumber(searched ?? 0, signal),
    enabled: searched !== null,
    retry: false, // a 404 (no such ticket) is an answer, not a network hiccup
  })

  const search = () => {
    const number = Number(numberText)
    if (Number.isInteger(number) && number > 0) setSearched(number)
  }

  const onReturned = (saleReturn: SaleReturn) => {
    setVoucher(saleReturn)
    setNumberText('')
    setSearched(null)
  }

  return (
    <div className="grid gap-6">
      <h1 className="text-2xl font-semibold">Devoluciones</h1>

      <form
        className="flex max-w-md items-end gap-2"
        onSubmit={(event) => {
          event.preventDefault()
          search()
        }}
      >
        <div className="grid flex-1 gap-2">
          <Label htmlFor="ticketNumber">Número del tiquete</Label>
          <Input
            id="ticketNumber"
            ref={numberRef}
            inputMode="numeric"
            autoFocus
            autoComplete="off"
            value={numberText}
            onChange={(event) => setNumberText(event.target.value)}
          />
        </div>
        <Button type="submit">Buscar</Button>
      </form>

      {searched !== null && sale.isPending && <p className="text-sm text-muted-foreground">Buscando el tiquete…</p>}
      {sale.isError && <p className="text-sm text-destructive">{sale.error.message}</p>}
      {searched !== null && sale.data && (
        // Shown even while refetching (e.g. when the window regains focus), so the cashier's choices are kept. A new
        // key only when the ticket or what was returned changes: then the form starts again from the server's numbers.
        <ReturnForm key={`${sale.data.id}:${sale.data.items.map((i) => i.returnedQuantity).join()}`} sale={sale.data} onReturned={onReturned} />
      )}

      <PrintDialog
        open={voucher !== null}
        title={voucher && voucher.creditedToAccount > 0 ? `Entregue ${formatMoney(voucher.cashRefund)} en efectivo` : 'Devolución registrada'}
        closeLabel="Listo"
        finalFocus={numberRef}
        onClose={() => setVoucher(null)}
      >
        {voucher && <ReturnVoucher saleReturn={voucher} />}
      </PrintDialog>
    </div>
  )
}

function ReturnForm({ sale, onReturned }: { sale: Sale; onReturned: (saleReturn: SaleReturn) => void }) {
  const queryClient = useQueryClient()
  const [draft, setDraft] = useState(() => draftFrom(sale))
  const [reason, setReason] = useState('')

  const refund = refundOf(draft)
  const problem = returnProblem(draft, reason)

  const mutation = useMutation({
    mutationFn: () => registerSaleReturn(toRequest(sale.id, reason, draft)),
    onSuccess: (saleReturn) => {
      // The ticket's returned quantities changed; the sale's detail and the shift's cash too.
      void queryClient.invalidateQueries({ queryKey: ['sales'] })
      onReturned(saleReturn)
    },
  })

  const edit = (next: typeof draft) => {
    mutation.reset()
    setDraft(next)
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>Tiquete N.º {sale.number}</CardTitle>
        <CardDescription>
          {formatDateTime(sale.soldAt)} · lo atendió {sale.soldByName} · total {formatMoney(sale.total)}
        </CardDescription>
      </CardHeader>
      <CardContent className="grid gap-5">
        <ul className="divide-y rounded-xl border" aria-label="Productos del tiquete">
          {draft.map((line) => {
            const label = `${line.productName}, ${line.saleUnitName}`
            const done = line.remaining === 0
            return (
              <li
                key={line.saleItemId}
                className={cn('grid gap-3 p-3 sm:grid-cols-[1fr_auto_auto_auto] sm:items-center', done && 'opacity-60')}
              >
                <div className="grid gap-0.5">
                  <span className="font-medium">{line.productName}</span>
                  <span className="text-sm text-muted-foreground">
                    {line.saleUnitName} a {formatMoney(line.unitPrice)} ·{' '}
                    {done ? 'ya se devolvió completo' : `quedan ${line.remaining} por devolver`}
                  </span>
                </div>

                {!done && (
                  <>
                    <div className="flex items-center gap-1">
                      <Button
                        variant="outline"
                        size="icon"
                        aria-label={`Devolver una unidad menos de ${label}`}
                        disabled={line.quantity === 0}
                        onClick={() => edit(setReturnQuantity(draft, line.saleItemId, line.quantity - 1))}
                      >
                        <Minus />
                      </Button>
                      <span className="w-8 text-center tabular-nums" aria-label={`Unidades a devolver de ${label}`}>
                        {line.quantity}
                      </span>
                      <Button
                        variant="outline"
                        size="icon"
                        aria-label={`Devolver una unidad más de ${label}`}
                        disabled={line.quantity >= line.remaining}
                        onClick={() => edit(setReturnQuantity(draft, line.saleItemId, line.quantity + 1))}
                      >
                        <Plus />
                      </Button>
                    </div>
                    <NativeSelect
                      size="sm"
                      aria-label={`Qué pasa con ${label}`}
                      value={line.disposition}
                      onChange={(event) => edit(setDisposition(draft, line.saleItemId, event.target.value as ReturnDisposition))}
                    >
                      {(['Restock', 'Discard'] as const).map((disposition) => (
                        <NativeSelectOption key={disposition} value={disposition}>
                          {dispositionLabels[disposition]}
                        </NativeSelectOption>
                      ))}
                    </NativeSelect>
                    <span className="min-w-24 text-right font-semibold tabular-nums">
                      {formatMoney(line.quantity * line.unitPrice)}
                    </span>
                  </>
                )}
              </li>
            )
          })}
        </ul>

        <div className="grid max-w-md gap-2">
          <Label htmlFor="returnReason">Motivo</Label>
          <Input
            id="returnReason"
            maxLength={200}
            autoComplete="off"
            placeholder="Por ejemplo: le formularon otro medicamento"
            value={reason}
            onChange={(event) => {
              mutation.reset()
              setReason(event.target.value)
            }}
          />
        </div>

        {mutation.isError && (
          <Alert variant="destructive" role="alert">
            <AlertDescription>{mutation.error.message}</AlertDescription>
          </Alert>
        )}

        {sale.creditAmount > 0 && (
          // How much goes to the debt depends on what the customer owes today: the server splits it (ADR 0010).
          <Alert>
            <AlertDescription>
              Este tiquete tuvo {formatMoney(sale.creditAmount)} fiados a {sale.customerName}. La devolución se descuenta
              primero de su deuda; el comprobante dirá cuánto efectivo entregar.
            </AlertDescription>
          </Alert>
        )}

        <div className="flex flex-wrap items-center justify-between gap-4 border-t pt-4">
          <div>
            <div className="text-sm text-muted-foreground">{sale.creditAmount > 0 ? 'Valor a devolver' : 'Efectivo a devolver'}</div>
            <div className="text-3xl font-semibold tabular-nums">{formatMoney(refund)}</div>
          </div>
          <div className="grid justify-items-end gap-1">
            <Button size="lg" disabled={problem !== null || mutation.isPending} onClick={() => mutation.mutate()}>
              {mutation.isPending ? 'Devolviendo…' : `Devolver ${formatMoney(refund)}`}
            </Button>
            {problem !== null && <span className="text-sm text-muted-foreground">{problem}</span>}
          </div>
        </div>
      </CardContent>
    </Card>
  )
}
