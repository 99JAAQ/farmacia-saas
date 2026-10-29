import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type KeyboardEvent, type Ref } from 'react'
import { Input } from '@/components/ui/input'
import { useDebouncedValue } from '@/lib/useDebouncedValue'
import { productKeys, searchProducts } from './api'
import { findScannedUnit, type ProductSelection } from './scan'

interface Props {
  id?: string
  ref?: Ref<HTMLInputElement>
  autoFocus?: boolean
  onSelect: (selection: ProductSelection) => void
}

/**
 * Type to search, or scan: a USB barcode reader "types" the code in milliseconds and presses Enter.
 * Enter searches right away (without waiting for the typing pause) so a scanned box is selected at once.
 */
export function ProductPicker({ id, ref, autoFocus, onSelect }: Props) {
  const queryClient = useQueryClient()
  const [search, setSearch] = useState('')
  const term = useDebouncedValue(search.trim())

  const results = useQuery({
    queryKey: productKeys.search(term),
    queryFn: ({ signal }) => searchProducts(term, signal),
    enabled: term.length >= 2,
  })

  const select = (selection: ProductSelection) => {
    onSelect(selection)
    setSearch('')
  }

  const onKeyDown = async (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key !== 'Enter') return
    event.preventDefault() // never submit the line form from the search box

    const text = search.trim()
    if (!text) return

    const found = await queryClient.fetchQuery({
      queryKey: productKeys.search(text),
      queryFn: ({ signal }) => searchProducts(text, signal),
    })
    const selection = findScannedUnit(found, text)
    if (selection) select(selection)
  }

  const showList = term.length >= 2

  return (
    <div className="grid gap-2">
      <Input
        id={id}
        ref={ref}
        autoFocus={autoFocus}
        placeholder="Escanee el código de barras o escriba el nombre…"
        value={search}
        onChange={(event) => setSearch(event.target.value)}
        onKeyDown={(event) => void onKeyDown(event)}
        autoComplete="off"
      />
      {showList && (
        <ul className="max-h-56 overflow-y-auto rounded-lg border" aria-label="Productos">
          {results.isPending && <li className="px-3 py-2 text-sm text-muted-foreground">Buscando…</li>}
          {results.isError && <li className="px-3 py-2 text-sm text-destructive">{results.error.message}</li>}
          {results.data?.length === 0 && <li className="px-3 py-2 text-sm text-muted-foreground">Ningún producto coincide.</li>}
          {results.data?.map((product) => (
            <li key={product.id}>
              <button
                type="button"
                disabled={product.saleUnits.length === 0}
                className="flex w-full justify-between gap-4 px-3 py-2 text-left text-sm hover:bg-muted focus-visible:bg-muted focus-visible:outline-none disabled:opacity-50"
                onClick={() => select({ product, saleUnit: product.saleUnits[0] })}
              >
                <span>
                  <span className="font-medium">{product.name}</span>
                  {product.activeIngredient && <span className="text-muted-foreground"> · {product.activeIngredient}</span>}
                </span>
                <span className="text-muted-foreground">{product.saleUnits.map((u) => u.name).join(' / ')}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
