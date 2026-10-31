import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { addSaleUnit, productKeys, updateSaleUnit, type Product, type SaleUnit, type SaleUnitInput } from '@/features/products/api'

/**
 * The product's presentations, each saved on its own: changing a price is one quick edit. A new price applies to
 * the next sales only (every sale kept its own); a new factor does not change the stock, which is in base units.
 */
export function SaleUnitsEditor({ product }: { product: Product }) {
  const [adding, setAdding] = useState(false)

  return (
    <div className="grid gap-3">
      <div className="hidden gap-2 px-1 text-xs text-muted-foreground sm:grid sm:grid-cols-[2fr_1fr_1fr_2fr_auto]">
        <span>Presentación</span>
        <span>{product.baseUnitName.toLowerCase()} que trae</span>
        <span>Precio</span>
        <span>Código de barras</span>
        <span className="w-20" />
      </div>
      {product.saleUnits.map((unit) => (
        // A new key when the saved unit changes: the row starts again from the server's values.
        <SaleUnitRow key={JSON.stringify(unit)} productId={product.id} unit={unit} />
      ))}
      {adding ? (
        <SaleUnitRow productId={product.id} onDone={() => setAdding(false)} />
      ) : (
        <Button variant="outline" size="sm" className="w-fit" onClick={() => setAdding(true)}>
          <Plus />
          Agregar presentación
        </Button>
      )}
    </div>
  )
}

/** One presentation: existing (edit) or new (add). */
function SaleUnitRow({ productId, unit, onDone }: { productId: string; unit?: SaleUnit; onDone?: () => void }) {
  const queryClient = useQueryClient()
  const [name, setName] = useState(unit?.name ?? '')
  const [factor, setFactor] = useState(String(unit?.conversionFactor ?? 1))
  const [price, setPrice] = useState(unit ? String(unit.price) : '')
  const [barcode, setBarcode] = useState(unit?.barcode ?? '')

  const input: SaleUnitInput = {
    name: name.trim(),
    conversionFactor: Number(factor),
    price: Number(price),
    barcode: barcode.trim() === '' ? null : barcode.trim(),
  }
  const valid = input.name !== '' && Number.isInteger(input.conversionFactor) && input.conversionFactor >= 1 && input.price > 0
  const changed =
    unit === undefined ||
    input.name !== unit.name ||
    input.conversionFactor !== unit.conversionFactor ||
    input.price !== unit.price ||
    input.barcode !== unit.barcode

  const mutation = useMutation({
    mutationFn: () => (unit ? updateSaleUnit(productId, unit.id, input) : addSaleUnit(productId, input).then(() => undefined)),
    // The product, its catalog line and the POS search show the presentations.
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: productKeys.all })
      onDone?.()
    },
  })

  const label = unit?.name ?? 'la presentación nueva'
  const edit = (setter: (value: string) => void) => (event: React.ChangeEvent<HTMLInputElement>) => {
    mutation.reset()
    setter(event.target.value)
  }

  return (
    <form
      className="grid gap-2 sm:grid-cols-[2fr_1fr_1fr_2fr_auto] sm:items-start"
      onSubmit={(event) => {
        event.preventDefault()
        if (valid && changed) mutation.mutate()
      }}
    >
      <Input aria-label={`Nombre de ${label}`} placeholder="Caja x30" autoFocus={unit === undefined} value={name} onChange={edit(setName)} />
      <Input aria-label={`Unidades que trae ${label}`} type="number" min={1} inputMode="numeric" value={factor} onChange={edit(setFactor)} />
      <Input aria-label={`Precio de ${label}`} type="number" min={0} step="any" inputMode="decimal" className="tabular-nums" value={price} onChange={edit(setPrice)} />
      <Input aria-label={`Código de barras de ${label}`} placeholder="Opcional" value={barcode} onChange={edit(setBarcode)} />
      <div className="flex gap-1">
        <Button type="submit" size="sm" className="w-20" disabled={!valid || !changed || mutation.isPending}>
          {unit ? 'Guardar' : 'Agregar'}
        </Button>
        {unit === undefined && (
          <Button type="button" size="sm" variant="ghost" onClick={onDone}>
            Cancelar
          </Button>
        )}
      </div>
      {mutation.isError && <p className="text-sm text-destructive sm:col-span-5">{mutation.error.message}</p>}
    </form>
  )
}
