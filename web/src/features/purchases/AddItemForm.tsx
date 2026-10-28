import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useRef, useState, type ReactNode } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { ProductPicker } from '@/features/products/ProductPicker'
import type { ProductSelection } from '@/features/products/scan'
import { formatMoney, todayLocal } from '@/lib/format'
import { addPurchaseItem, purchaseKeys } from './api'
import { vatRateLabels } from './labels'

const number = (message: string) => z.number({ error: message }).min(0, { error: 'No puede ser negativo.' })

// The domain's rules, shown next to the field before sending (the API validates them again).
const schema = z
  .object({
    saleUnitId: z.string().min(1),
    quantity: number('Escriba la cantidad.').int({ error: 'Debe ser un número entero.' }),
    bonusQuantity: number('Escriba la bonificación (0 si no hay).').int({ error: 'Debe ser un número entero.' }),
    unitCost: number('Escriba el costo.'),
    lineDiscount: number('Escriba el descuento (0 si no hay).'),
    vatRate: z.enum(['Exempt', 'Reduced', 'Standard']),
    lotNumber: z.string().trim().min(1, { error: 'Escriba el lote.' }),
    expirationDate: z.string().min(1, { error: 'Escriba el vencimiento.' }),
  })
  .superRefine((line, ctx) => {
    if (line.quantity + line.bonusQuantity < 1)
      ctx.addIssue({ code: 'custom', path: ['quantity'], message: 'Debe haber al menos una unidad comprada o bonificada.' })
    // UX guard: free units go in "Bonificación", so a bought quantity at $0 is almost always a typo.
    if (line.quantity > 0 && line.unitCost === 0)
      ctx.addIssue({ code: 'custom', path: ['unitCost'], message: 'El costo no puede ser $0. Las unidades gratis van en Bonificación.' })
    if (line.lineDiscount > line.quantity * line.unitCost)
      ctx.addIssue({ code: 'custom', path: ['lineDiscount'], message: 'El descuento no puede ser mayor que el valor de la línea.' })
    if (line.expirationDate && line.expirationDate < todayLocal())
      ctx.addIssue({ code: 'custom', path: ['expirationDate'], message: 'Ese lote ya está vencido: no se puede recibir.' })
  })

type LineForm = z.infer<typeof schema>

const emptyLine: Omit<LineForm, 'saleUnitId'> = {
  quantity: 1,
  bonusQuantity: 0,
  unitCost: 0,
  lineDiscount: 0,
  vatRate: 'Exempt',
  lotNumber: '',
  expirationDate: '',
}

export function AddItemForm({ purchaseId }: { purchaseId: string }) {
  const queryClient = useQueryClient()
  const searchRef = useRef<HTMLInputElement>(null)
  const [selection, setSelection] = useState<ProductSelection | null>(null)

  const { register, handleSubmit, control, reset, setFocus, formState: { errors } } = useForm<LineForm>({
    resolver: zodResolver(schema),
    defaultValues: { saleUnitId: '', ...emptyLine },
  })

  const [quantity, bonusQuantity, unitCost, lineDiscount, saleUnitId] = useWatch({
    control,
    name: ['quantity', 'bonusQuantity', 'unitCost', 'lineDiscount', 'saleUnitId'],
  })

  const mutation = useMutation({
    mutationFn: (line: LineForm) => addPurchaseItem(purchaseId, { productId: selection!.product.id, ...line }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: purchaseKeys.detail(purchaseId) })
      // Ready for the next line: clean form, cursor back in the product search (or the scanner).
      setSelection(null)
      reset({ saleUnitId: '', ...emptyLine })
      searchRef.current?.focus()
    },
  })

  const choose = (chosen: ProductSelection) => {
    setSelection(chosen)
    reset({ saleUnitId: chosen.saleUnit.id, ...emptyLine })
    setTimeout(() => setFocus('quantity'), 0) // after the fields render
  }

  // Trivial arithmetic for the line being typed; the invoice totals always come from the server.
  const factor = selection?.product.saleUnits.find((u) => u.id === saleUnitId)?.conversionFactor ?? 0
  const preview = Number.isFinite(quantity * unitCost - lineDiscount) ? quantity * unitCost - lineDiscount : 0
  const units = Number.isFinite((quantity + bonusQuantity) * factor) ? (quantity + bonusQuantity) * factor : 0

  return (
    <section className="grid gap-4 rounded-lg border p-4" aria-labelledby="add-line-title">
      <h2 id="add-line-title" className="font-medium">Agregar línea</h2>

      <ProductPicker id="product-search" ref={searchRef} onSelect={choose} />

      {selection && (
        <form
          onSubmit={handleSubmit((line) => mutation.mutate(line))}
          noValidate
          className="grid gap-4"
          aria-label={`Línea de ${selection.product.name}`}
        >
          {mutation.isError && (
            <Alert variant="destructive" role="alert">
              <AlertDescription>{mutation.error.message}</AlertDescription>
            </Alert>
          )}

          <div className="flex flex-wrap items-end gap-4">
            <div className="grid min-w-56 flex-1 gap-1">
              <span className="text-sm text-muted-foreground">Producto</span>
              <span className="font-medium">{selection.product.name}</span>
            </div>
            <Field label="Presentación" id="saleUnitId">
              <NativeSelect id="saleUnitId" {...register('saleUnitId')}>
                {selection.product.saleUnits.map((unit) => (
                  <NativeSelectOption key={unit.id} value={unit.id}>
                    {unit.name} ({unit.conversionFactor} {selection.product.baseUnitName.toLowerCase()})
                  </NativeSelectOption>
                ))}
              </NativeSelect>
            </Field>
          </div>

          <div className="grid gap-4 sm:grid-cols-4">
            <Field label="Cantidad" id="quantity" error={errors.quantity?.message}>
              <Input id="quantity" type="number" min={0} inputMode="numeric" {...register('quantity', { valueAsNumber: true })} />
            </Field>
            <Field label="Bonificación" id="bonusQuantity" error={errors.bonusQuantity?.message}>
              <Input id="bonusQuantity" type="number" min={0} inputMode="numeric" {...register('bonusQuantity', { valueAsNumber: true })} />
            </Field>
            <Field label="Costo unitario" id="unitCost" error={errors.unitCost?.message}>
              <Input id="unitCost" type="number" min={0} step="any" inputMode="decimal" {...register('unitCost', { valueAsNumber: true })} />
            </Field>
            <Field label="Descuento de línea" id="lineDiscount" error={errors.lineDiscount?.message}>
              <Input id="lineDiscount" type="number" min={0} step="any" inputMode="decimal" {...register('lineDiscount', { valueAsNumber: true })} />
            </Field>
          </div>

          <div className="grid gap-4 sm:grid-cols-4">
            <Field label="IVA" id="vatRate">
              <NativeSelect id="vatRate" {...register('vatRate')}>
                {(['Exempt', 'Reduced', 'Standard'] as const).map((rate) => (
                  <NativeSelectOption key={rate} value={rate}>{vatRateLabels[rate]}</NativeSelectOption>
                ))}
              </NativeSelect>
            </Field>
            <Field label="Lote" id="lotNumber" error={errors.lotNumber?.message}>
              <Input id="lotNumber" autoComplete="off" {...register('lotNumber')} />
            </Field>
            <Field label="Vencimiento" id="expirationDate" error={errors.expirationDate?.message}>
              <Input id="expirationDate" type="date" min={todayLocal()} {...register('expirationDate')} />
            </Field>
            <div className="grid content-end gap-1 text-sm" aria-live="polite">
              <span className="text-muted-foreground">Valor de la línea</span>
              <span className="font-medium">{formatMoney(preview)} · {units} {selection.product.baseUnitName.toLowerCase()}</span>
            </div>
          </div>

          <div className="flex gap-2">
            <Button type="submit" disabled={mutation.isPending}>
              {mutation.isPending ? 'Guardando…' : 'Agregar línea'}
            </Button>
            <Button type="button" variant="ghost" onClick={() => { setSelection(null); searchRef.current?.focus() }}>
              Cancelar
            </Button>
          </div>
        </form>
      )}
    </section>
  )
}

function Field({ label, id, error, children }: { label: string; id: string; error?: string; children: ReactNode }) {
  return (
    <div className="grid gap-2">
      <Label htmlFor={id}>{label}</Label>
      {children}
      {error && <p className="text-sm text-destructive">{error}</p>}
    </div>
  )
}
