import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { formatStock, pluralize } from '@/features/catalog/stock'
import { productKeys, type Product } from '@/features/products/api'
import { setMinimumStock } from './alerts'

/**
 * The minimum is stored in base units (tablets), but nobody thinks of "60 tablets": the user types it in a
 * presentation ("2 Caja x30") and the screen converts. Remounted (key) when the saved minimum changes.
 */
export function MinimumStockEditor({ product }: { product: Product }) {
  const queryClient = useQueryClient()
  const largest = product.saleUnits[0] // the API orders them largest first
  const [unitId, setUnitId] = useState(largest?.id ?? '')
  const unit = product.saleUnits.find((u) => u.id === unitId) ?? largest
  const [quantityText, setQuantityText] = useState(
    product.minimumStock && unit ? String(product.minimumStock / unit.conversionFactor) : '',
  )

  const quantity = Number(quantityText)
  const baseUnits = quantityText.trim() === '' ? null : Math.round(quantity * (unit?.conversionFactor ?? 1))
  const invalid = quantityText.trim() !== '' && (!Number.isFinite(quantity) || quantity < 0 || !Number.isInteger(baseUnits))

  const mutation = useMutation({
    mutationFn: (value: number | null) => setMinimumStock(product.id, value),
    // The product's detail and the alerts both live under ['products', …].
    onSuccess: () => queryClient.invalidateQueries({ queryKey: productKeys.all }),
  })

  return (
    <form
      className="grid gap-3"
      onSubmit={(event) => {
        event.preventDefault()
        if (!invalid) mutation.mutate(baseUnits === 0 ? null : baseUnits)
      }}
    >
      <p className="text-sm text-muted-foreground">
        {product.minimumStock
          ? `Mínimo actual: ${formatStock(product.minimumStock, product.saleUnits, product.baseUnitName)}. Por debajo, aparece en Alertas.`
          : 'Sin mínimo: este producto no genera alertas de stock bajo.'}
      </p>
      <div className="flex flex-wrap items-end gap-2">
        <div className="grid gap-2">
          <Label htmlFor="minimum-quantity">Cantidad</Label>
          <Input id="minimum-quantity" className="w-28" inputMode="decimal" value={quantityText}
            aria-invalid={invalid ? true : undefined}
            onChange={(event) => { mutation.reset(); setQuantityText(event.target.value) }} />
        </div>
        <div className="grid gap-2">
          <Label htmlFor="minimum-unit">Presentación</Label>
          <NativeSelect id="minimum-unit" value={unit?.id ?? ''} onChange={(event) => setUnitId(event.target.value)}>
            {product.saleUnits.map((option) => (
              <NativeSelectOption key={option.id} value={option.id}>{option.name}</NativeSelectOption>
            ))}
          </NativeSelect>
        </div>
        <Button type="submit" variant="outline" disabled={invalid || mutation.isPending}>
          {mutation.isPending ? 'Guardando…' : 'Guardar mínimo'}
        </Button>
      </div>
      {invalid && <p className="text-sm text-destructive">Debe dar un número entero de {pluralize(product.baseUnitName, 2).toLowerCase()}.</p>}
      {mutation.isError && <p className="text-sm text-destructive">{mutation.error.message}</p>}
      {quantityText.trim() !== '' && !invalid && baseUnits !== null && unit && unit.conversionFactor > 1 && (
        <p className="text-xs text-muted-foreground">= {baseUnits} {pluralize(product.baseUnitName, baseUnits).toLowerCase()}. Deje vacío o en 0 para quitarlo.</p>
      )}
    </form>
  )
}
