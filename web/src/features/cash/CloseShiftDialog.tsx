import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { formatMoney } from '@/lib/format'
import { cn } from '@/lib/utils'
import { cashKeys, closeCashSession, type CashSession } from './api'
import { describeDifference } from './labels'

/**
 * Blind count: asks for the cash counted by hand without showing the expected amount (the server does not send it
 * to the cashier either). Only after closing does it show expected, counted and the difference.
 */
export function CloseShiftDialog({ session, label = 'Cerrar turno' }: { session: CashSession; label?: string }) {
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [countedText, setCountedText] = useState('')
  const [notes, setNotes] = useState('')
  const counted = Number(countedText)
  const valid = countedText.trim() !== '' && Number.isFinite(counted) && counted >= 0

  const mutation = useMutation({
    mutationFn: () => closeCashSession(session.id, counted, notes.trim() || null),
  })
  const closed = mutation.data

  const onOpenChange = (next: boolean) => {
    setOpen(next)
    if (next) {
      setCountedText('')
      setNotes('')
      mutation.reset()
    } else if (closed) {
      // Only now, after the cashier has read the result, does the page go back to "register closed".
      void queryClient.invalidateQueries({ queryKey: cashKeys.current })
    }
  }

  return (
    <>
      <Button size="lg" onClick={() => onOpenChange(true)}>{label}</Button>
      <Dialog open={open} onOpenChange={onOpenChange}>
        <DialogContent>
          {closed ? <ClosingResult session={closed} /> : (
            <>
              <DialogHeader>
                <DialogTitle>Cerrar turno de {session.openedByName}</DialogTitle>
                <DialogDescription>
                  Cuente todo el efectivo del cajón, incluida la base, y escriba el total. Después de cerrar verá
                  cuánto debía haber y la diferencia.
                </DialogDescription>
              </DialogHeader>
              <form
                id="close-shift"
                className="grid gap-3"
                onSubmit={(event) => {
                  event.preventDefault()
                  if (valid) mutation.mutate()
                }}
              >
                {mutation.isError && (
                  <Alert variant="destructive" role="alert">
                    <AlertDescription>{mutation.error.message}</AlertDescription>
                  </Alert>
                )}
                <div className="grid gap-2">
                  <Label htmlFor="countedCash">Efectivo contado</Label>
                  <Input id="countedCash" type="number" min={0} step="any" inputMode="decimal" autoFocus
                    value={countedText} onChange={(event) => setCountedText(event.target.value)} />
                </div>
                <div className="grid gap-2">
                  <Label htmlFor="closingNotes">Observaciones (opcional)</Label>
                  <Input id="closingNotes" maxLength={500} autoComplete="off"
                    value={notes} onChange={(event) => setNotes(event.target.value)} />
                </div>
              </form>
            </>
          )}
          <DialogFooter>
            {closed ? (
              <Button onClick={() => onOpenChange(false)}>Listo</Button>
            ) : (
              <Button type="submit" form="close-shift" disabled={!valid || mutation.isPending}>
                {mutation.isPending ? 'Cerrando…' : 'Cerrar turno'}
              </Button>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}

function ClosingResult({ session }: { session: CashSession }) {
  const difference = describeDifference(session.difference ?? 0)

  return (
    <>
      <DialogHeader>
        <DialogTitle>Turno cerrado</DialogTitle>
        <DialogDescription>Así quedó el cierre del turno de {session.openedByName}.</DialogDescription>
      </DialogHeader>
      <dl className="grid gap-1 text-sm">
        <div className="flex justify-between">
          <dt className="text-muted-foreground">Debía haber</dt>
          <dd>{formatMoney(session.expectedCash ?? 0)}</dd>
        </div>
        <div className="flex justify-between">
          <dt className="text-muted-foreground">Contado</dt>
          <dd>{formatMoney(session.countedCash ?? 0)}</dd>
        </div>
      </dl>
      <p
        role="status"
        className={cn(
          'rounded-md p-3 text-center font-medium',
          difference.kind === 'exact' && 'bg-muted',
          difference.kind === 'surplus' && 'bg-warning/10 text-warning',
          difference.kind === 'shortage' && 'bg-destructive/10 text-destructive',
        )}
      >
        {difference.text}
      </p>
    </>
  )
}
