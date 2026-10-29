import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useEffectEvent, useRef, useState } from 'react'
import { Link } from 'react-router'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button, buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useAuth } from '@/features/auth/useAuth'
import { cashKeys, getCurrentCashSession, type CashSession } from '@/features/cash/api'
import { ProductPicker } from '@/features/products/ProductPicker'
import type { User } from '@/lib/api/types'
import { formatMoney } from '@/lib/format'
import { registerSale, saleKeys, type Sale } from './api'
import { addToCart, changeUnit, emptyCart, estimatedTotal, needsPrescription, removeLine, setQuantity, type Cart } from './cart'
import { CartTable } from './CartTable'
import { PaymentPanel } from './PaymentPanel'
import { summarizePayment, toAmount, type NonCashRow } from './payment'
import { ShiftSales } from './ShiftSales'
import { TicketDialog } from './Ticket'
import { useCart } from './useCart'

/** Selling needs a shift of one's own: the cash of every sale belongs to whoever opened the register. */
export function SellPage() {
  const { user } = useAuth()
  const current = useQuery({
    queryKey: cashKeys.current,
    queryFn: ({ signal }) => getCurrentCashSession(signal),
  })

  if (current.isPending) return <p className="text-sm text-muted-foreground">Cargando…</p>
  if (current.isError) return <p className="text-sm text-destructive">{current.error.message}</p>

  const { session } = current.data
  if (session === null || !session.isMine)
    return (
      <Card className="max-w-lg">
        <CardHeader>
          <CardTitle>{session === null ? 'La caja está cerrada' : `La caja está abierta por ${session.openedByName}`}</CardTitle>
          <CardDescription>
            {session === null
              ? 'Para vender, primero abra su turno con la base de efectivo.'
              : 'Solo quien abrió el turno puede vender en él. Si va a atender usted, ese turno debe cerrarse y usted abrir el suyo.'}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Link to="/caja" className={buttonVariants()}>
            Ir a Caja
          </Link>
        </CardContent>
      </Card>
    )

  return user && <PointOfSale user={user} session={session} />
}

function PointOfSale({ user, session }: { user: User; session: CashSession }) {
  const queryClient = useQueryClient()
  const searchRef = useRef<HTMLInputElement>(null)
  const [cart, setCart] = useCart(user)
  const [rows, setRows] = useState<NonCashRow[]>([])
  const [cashText, setCashText] = useState('')
  const [prescriptionChecked, setPrescriptionChecked] = useState(false)
  const [ticket, setTicket] = useState<Sale | null>(null)

  const total = estimatedTotal(cart)
  const summary = summarizePayment(
    total,
    rows.map((row) => ({ method: row.method, amount: toAmount(row.text) })),
    cashText.trim() === '' ? null : toAmount(cashText),
  )
  const prescriptionNeeded = needsPrescription(cart)
  const problem =
    summary.problem ?? (prescriptionNeeded && !prescriptionChecked ? 'Marque que revisó la fórmula médica.' : null)

  const mutation = useMutation({
    mutationFn: registerSale,
    onSuccess: (sale) => {
      setTicket(sale)
      setCart(emptyCart)
      setRows([])
      setCashText('')
      setPrescriptionChecked(false)
      void queryClient.invalidateQueries({ queryKey: saleKeys.byShift(session.id) })
    },
  })

  // Any change to the sale makes an earlier error stale (e.g. "not enough stock" after lowering the quantity).
  const edit = (next: (cart: Cart) => Cart) => {
    mutation.reset()
    setCart(next)
  }

  const charge = () => {
    if (problem !== null || mutation.isPending) return
    mutation.mutate({
      lines: cart.map(({ productId, saleUnitId, quantity }) => ({ productId, saleUnitId, quantity })),
      payments: summary.payments,
      prescriptionChecked: prescriptionNeeded && prescriptionChecked,
    })
  }

  // F2 charges from anywhere on the page (the cashier's hands stay on the keyboard and the scanner); Esc goes back
  // to the product search. Esc never empties the cart: too easy to press by mistake.
  const onKeyDown = useEffectEvent((event: KeyboardEvent) => {
    if (ticket !== null) return // the ticket dialog handles its own keys
    if (event.key === 'F2') {
      event.preventDefault()
      charge()
    } else if (event.key === 'Escape') {
      searchRef.current?.focus()
    }
  })
  useEffect(() => {
    const listener = (event: KeyboardEvent) => onKeyDown(event)
    window.addEventListener('keydown', listener)
    return () => window.removeEventListener('keydown', listener)
  }, [])

  return (
    <div className="grid gap-6">
      <div className="flex items-baseline justify-between gap-4">
        <h1 className="text-2xl font-semibold">Vender</h1>
        <span className="text-sm text-muted-foreground">
          F2 cobra · Esc vuelve al buscador
        </span>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_22rem] lg:items-start">
        <section aria-label="Productos" className="grid gap-4">
          <ProductPicker
            ref={searchRef}
            id="pos-search"
            autoFocus
            onSelect={(selection) => edit((current) => addToCart(current, selection))}
          />
          <CartTable
            cart={cart}
            onQuantity={(line, quantity) => edit((current) => setQuantity(current, line.productId, line.saleUnitId, quantity))}
            onUnit={(line, unitId) => edit((current) => changeUnit(current, line.productId, line.saleUnitId, unitId))}
            onRemove={(line) => edit((current) => removeLine(current, line.productId, line.saleUnitId))}
          />
          {cart.length > 0 && (
            <div className="flex justify-end">
              <Button variant="ghost" size="sm" onClick={() => edit(() => emptyCart)}>
                Vaciar la venta
              </Button>
            </div>
          )}
        </section>

        <Card className="lg:sticky lg:top-8">
          <CardHeader>
            <CardDescription>Total estimado</CardDescription>
            <CardTitle className="text-4xl font-semibold tabular-nums">{formatMoney(total)}</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4">
            <PaymentPanel
              summary={summary}
              cashText={cashText}
              onCashText={(text) => {
                mutation.reset()
                setCashText(text)
              }}
              rows={rows}
              onRows={(next) => {
                mutation.reset()
                setRows(next)
              }}
            />

            {prescriptionNeeded && (
              <label className="flex items-start gap-3 rounded-lg border border-warning/40 bg-warning/10 p-3 text-sm">
                <input
                  type="checkbox"
                  className="mt-0.5 size-4 accent-primary"
                  checked={prescriptionChecked}
                  onChange={(event) => setPrescriptionChecked(event.target.checked)}
                />
                <span>
                  <span className="font-medium">Fórmula revisada.</span> Hay productos que requieren fórmula médica.
                </span>
              </label>
            )}

            {mutation.isError && (
              <Alert variant="destructive" role="alert">
                <AlertDescription>{mutation.error.message}</AlertDescription>
              </Alert>
            )}

            <Button size="lg" className="h-12 text-base" disabled={problem !== null || mutation.isPending} onClick={charge}>
              {mutation.isPending ? 'Cobrando…' : 'Cobrar (F2)'}
            </Button>
            {problem !== null && cart.length > 0 && <p className="text-center text-sm text-muted-foreground">{problem}</p>}
          </CardContent>
        </Card>
      </div>

      <section aria-labelledby="shift-sales" className="grid gap-3">
        <h2 id="shift-sales" className="text-lg font-semibold">
          Ventas de este turno
        </h2>
        <ShiftSales cashSessionId={session.id} />
      </section>

      <TicketDialog sale={ticket} finalFocus={searchRef} onClose={() => setTicket(null)} />
    </div>
  )
}
