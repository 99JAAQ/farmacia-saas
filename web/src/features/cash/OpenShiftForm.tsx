import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { formatMoney } from '@/lib/format'
import { cashKeys, openCashSession } from './api'

/** The register is closed: open a shift, counting the float. It starts with what the last closing left. */
export function OpenShiftForm({ suggestedOpeningFloat }: { suggestedOpeningFloat: number }) {
  const queryClient = useQueryClient()
  const [value, setValue] = useState(String(suggestedOpeningFloat))
  const amount = Number(value)
  const valid = value.trim() !== '' && Number.isFinite(amount) && amount >= 0

  const mutation = useMutation({
    mutationFn: () => openCashSession(amount),
    // Also on error: a 409 means someone else opened it meanwhile, and the page must show their shift.
    onSettled: () => queryClient.invalidateQueries({ queryKey: cashKeys.current }),
  })

  return (
    <Card className="max-w-md">
      <CardHeader>
        <CardTitle>La caja está cerrada</CardTitle>
        <CardDescription>
          Cuente el efectivo del cajón y escriba la base con la que empieza su turno.
          {suggestedOpeningFloat > 0 && ` Al último cierre quedaron ${formatMoney(suggestedOpeningFloat)}.`}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form
          className="grid gap-4"
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
            <Label htmlFor="openingFloat">Base inicial</Label>
            <Input id="openingFloat" type="number" min={0} step="any" inputMode="decimal" autoFocus
              value={value} onChange={(event) => setValue(event.target.value)} aria-invalid={valid ? undefined : true} />
          </div>
          <Button type="submit" size="lg" disabled={!valid || mutation.isPending}>
            {mutation.isPending ? 'Abriendo…' : 'Abrir turno'}
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}
