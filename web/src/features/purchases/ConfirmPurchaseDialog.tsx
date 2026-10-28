import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { confirmPurchase, purchaseKeys } from './api'

/**
 * Asks for the total printed on the paper invoice. It deliberately does NOT show the calculated total:
 * the user would just copy it and the check would catch nothing. Only when they differ does the server's
 * message show both amounts.
 */
export function ConfirmPurchaseDialog({ purchaseId, disabled }: { purchaseId: string; disabled: boolean }) {
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [value, setValue] = useState('')
  const amount = Number(value)
  const valid = value.trim() !== '' && Number.isFinite(amount) && amount >= 0

  const mutation = useMutation({
    mutationFn: () => confirmPurchase(purchaseId, amount),
    onSuccess: (confirmed) => {
      queryClient.setQueryData(purchaseKeys.detail(purchaseId), confirmed)
      void queryClient.invalidateQueries({ queryKey: purchaseKeys.all })
      setOpen(false)
    },
  })

  const onOpenChange = (next: boolean) => {
    setOpen(next)
    if (next) {
      setValue('')
      mutation.reset()
    }
  }

  return (
    <>
      <Button size="lg" disabled={disabled} onClick={() => onOpenChange(true)}>
        Confirmar recepción
      </Button>
      <Dialog open={open} onOpenChange={onOpenChange}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Confirmar recepción</DialogTitle>
            <DialogDescription>
              Escriba el total que aparece impreso en la factura del proveedor. Al confirmar, los lotes entran al
              inventario y la compra ya no se puede editar.
            </DialogDescription>
          </DialogHeader>

          <form
            id="confirm-purchase"
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
              <Label htmlFor="printedTotal">Total de la factura impresa</Label>
              <Input id="printedTotal" type="number" min={0} step="any" inputMode="decimal" autoFocus
                value={value} onChange={(event) => setValue(event.target.value)} />
            </div>
          </form>

          <DialogFooter>
            <Button type="submit" form="confirm-purchase" disabled={!valid || mutation.isPending}>
              {mutation.isPending ? 'Confirmando…' : 'Confirmar'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  )
}
