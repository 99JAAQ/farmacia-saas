import { Minus, Plus, ScanBarcode, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { formatMoney } from '@/lib/format'
import { lineTotal, unitOf, type Cart, type CartLine } from './cart'

interface Props {
  cart: Cart
  onQuantity: (line: CartLine, quantity: number) => void
  onUnit: (line: CartLine, saleUnitId: string) => void
  onRemove: (line: CartLine) => void
}

export function CartTable({ cart, onQuantity, onUnit, onRemove }: Props) {
  if (cart.length === 0)
    return (
      <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed py-14 text-center text-muted-foreground">
        <ScanBarcode className="size-8" aria-hidden="true" />
        <p className="text-sm">Escanee un producto o búsquelo por nombre para empezar la venta.</p>
      </div>
    )

  return (
    <ul className="divide-y rounded-xl border bg-card" aria-label="Productos de la venta">
      {cart.map((line) => {
        const unit = unitOf(line)
        const label = `${line.productName}, ${unit.name}`
        return (
          <li key={`${line.productId}:${line.saleUnitId}`} className="grid gap-3 p-3 sm:grid-cols-[1fr_auto_auto] sm:items-center">
            <div className="grid gap-1">
              <div className="flex flex-wrap items-center gap-2">
                <span className="font-medium">{line.productName}</span>
                {line.requiresPrescription && <Badge variant="outline">Fórmula</Badge>}
              </div>
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                {line.units.length > 1 ? (
                  <NativeSelect
                    size="sm"
                    aria-label={`Presentación de ${line.productName}`}
                    value={line.saleUnitId}
                    onChange={(event) => onUnit(line, event.target.value)}
                  >
                    {line.units.map((option) => (
                      <NativeSelectOption key={option.id} value={option.id}>
                        {option.name} · {formatMoney(option.price)}
                      </NativeSelectOption>
                    ))}
                  </NativeSelect>
                ) : (
                  <span>
                    {unit.name} · {formatMoney(unit.price)}
                  </span>
                )}
              </div>
            </div>

            <div className="flex items-center gap-1">
              <Button
                variant="outline"
                size="icon"
                aria-label={`Quitar una unidad de ${label}`}
                disabled={line.quantity <= 1}
                onClick={() => onQuantity(line, line.quantity - 1)}
              >
                <Minus />
              </Button>
              <QuantityInput
                quantity={line.quantity}
                label={`Cantidad de ${label}`}
                onChange={(quantity) => onQuantity(line, quantity)}
              />
              <Button
                variant="outline"
                size="icon"
                aria-label={`Agregar una unidad de ${label}`}
                onClick={() => onQuantity(line, line.quantity + 1)}
              >
                <Plus />
              </Button>
            </div>

            <div className="flex items-center justify-between gap-2 sm:justify-end">
              <span className="min-w-24 text-right font-semibold tabular-nums">{formatMoney(lineTotal(line))}</span>
              <Button variant="ghost" size="icon" aria-label={`Quitar ${label} de la venta`} onClick={() => onRemove(line)}>
                <Trash2 />
              </Button>
            </div>
          </li>
        )
      })}
    </ul>
  )
}

/**
 * While focused it keeps its own text, so the cashier can clear the field to type another number; a valid whole
 * number goes to the cart at once. Unfocused, it shows the cart's quantity (changed by +/− or a rescan).
 */
function QuantityInput({ quantity, label, onChange }: { quantity: number; label: string; onChange: (quantity: number) => void }) {
  const [text, setText] = useState<string | null>(null)

  return (
    <Input
      aria-label={label}
      inputMode="numeric"
      className="w-14 text-center tabular-nums"
      value={text ?? String(quantity)}
      onFocus={(event) => {
        setText(String(quantity))
        event.target.select()
      }}
      onChange={(event) => {
        setText(event.target.value)
        const typed = Number(event.target.value)
        if (event.target.value.trim() !== '' && Number.isInteger(typed) && typed >= 1) onChange(typed)
      }}
      onBlur={() => setText(null)}
    />
  )
}
