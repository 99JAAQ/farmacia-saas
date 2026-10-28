import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { purchaseKeys, setInvoiceDiscount } from './api'

/** Discount on the whole invoice; the server spreads it over the lines and recalculates the VAT. */
export function InvoiceDiscountForm({ purchaseId, current }: { purchaseId: string; current: number }) {
  const queryClient = useQueryClient()
  const [value, setValue] = useState(String(current))
  const amount = Number(value)
  const valid = value.trim() !== '' && Number.isFinite(amount) && amount >= 0

  const mutation = useMutation({
    mutationFn: () => setInvoiceDiscount(purchaseId, amount),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: purchaseKeys.detail(purchaseId) }),
  })

  return (
    <form
      className="grid gap-2"
      onSubmit={(event) => {
        event.preventDefault()
        if (valid) mutation.mutate()
      }}
    >
      <Label htmlFor="invoiceDiscount">Descuento de la factura</Label>
      <div className="flex gap-2">
        <Input id="invoiceDiscount" type="number" min={0} step="any" inputMode="decimal" className="w-40"
          value={value} onChange={(event) => setValue(event.target.value)} aria-invalid={valid ? undefined : true} />
        <Button type="submit" variant="outline" disabled={!valid || amount === current || mutation.isPending}>
          {mutation.isPending ? 'Aplicando…' : 'Aplicar'}
        </Button>
      </div>
      {mutation.isError && <p className="text-sm text-destructive">{mutation.error.message}</p>}
    </form>
  )
}
