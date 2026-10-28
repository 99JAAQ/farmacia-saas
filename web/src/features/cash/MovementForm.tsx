import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { cashKeys, recordCashMovement, type CashMovementType } from './api'
import { movementTypeLabels } from './labels'

/** Cash in or out that is not a sale: a delivery, an owner withdrawal, change brought from the bank. */
export function MovementForm() {
  const queryClient = useQueryClient()
  const [type, setType] = useState<CashMovementType>('Expense')
  const [amountText, setAmountText] = useState('')
  const [reason, setReason] = useState('')
  const amount = Number(amountText)
  const valid = amountText.trim() !== '' && Number.isFinite(amount) && amount > 0 && reason.trim() !== ''

  const mutation = useMutation({
    mutationFn: () => recordCashMovement(type, amount, reason.trim()),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: cashKeys.current })
      setAmountText('')
      setReason('')
    },
  })

  return (
    <form
      className="grid gap-3 rounded-lg border p-4"
      aria-labelledby="movement-title"
      onSubmit={(event) => {
        event.preventDefault()
        if (valid) mutation.mutate()
      }}
    >
      <h2 id="movement-title" className="font-medium">Registrar ingreso o egreso</h2>
      <div className="grid gap-3 sm:grid-cols-[10rem_10rem_1fr_auto] sm:items-end">
        <div className="grid gap-2">
          <Label htmlFor="movementType">Tipo</Label>
          <NativeSelect id="movementType" value={type} onChange={(event) => setType(event.target.value as CashMovementType)}>
            {(['Expense', 'Income'] as const).map((option) => (
              <NativeSelectOption key={option} value={option}>{movementTypeLabels[option]}</NativeSelectOption>
            ))}
          </NativeSelect>
        </div>
        <div className="grid gap-2">
          <Label htmlFor="movementAmount">Valor</Label>
          <Input id="movementAmount" type="number" min={0} step="any" inputMode="decimal"
            value={amountText} onChange={(event) => setAmountText(event.target.value)} />
        </div>
        <div className="grid gap-2">
          <Label htmlFor="movementReason">Motivo</Label>
          <Input id="movementReason" maxLength={200} autoComplete="off" placeholder="Domicilio, retiro del dueño…"
            value={reason} onChange={(event) => setReason(event.target.value)} />
        </div>
        <Button type="submit" variant="outline" disabled={!valid || mutation.isPending}>
          {mutation.isPending ? 'Guardando…' : 'Registrar'}
        </Button>
      </div>
      {mutation.isError && <p className="text-sm text-destructive" role="alert">{mutation.error.message}</p>}
    </form>
  )
}
