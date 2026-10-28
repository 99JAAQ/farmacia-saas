import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { useDebouncedValue } from '@/lib/useDebouncedValue'
import { cn } from '@/lib/utils'
import { searchSuppliers } from './api'
import { CreateSupplierDialog } from './CreateSupplierDialog'

export interface SelectedSupplier {
  id: string
  name: string
}

interface Props {
  id?: string
  value: SelectedSupplier | null
  onChange: (supplier: SelectedSupplier | null) => void
  invalid?: boolean
}

/** Search by name or NIT (accents do not matter), or create the supplier if it is the first time it delivers. */
export function SupplierPicker({ id, value, onChange, invalid }: Props) {
  const [search, setSearch] = useState('')
  const [creating, setCreating] = useState(false)
  const term = useDebouncedValue(search.trim())

  const results = useQuery({
    queryKey: ['suppliers', term],
    queryFn: ({ signal }) => searchSuppliers(term, signal),
    enabled: value === null,
  })

  if (value)
    return (
      <div className="flex items-center justify-between gap-2 rounded-lg border px-3 py-2">
        <span className="font-medium">{value.name}</span>
        <Button type="button" variant="ghost" size="sm" onClick={() => onChange(null)}>
          Cambiar
        </Button>
      </div>
    )

  return (
    <div className="grid gap-2">
      <Input
        id={id}
        placeholder="Buscar por nombre o NIT…"
        value={search}
        onChange={(event) => setSearch(event.target.value)}
        aria-invalid={invalid ? true : undefined}
        autoComplete="off"
      />
      <ul className="max-h-48 overflow-y-auto rounded-lg border" aria-label="Proveedores">
        {results.isPending && <li className="px-3 py-2 text-sm text-muted-foreground">Buscando…</li>}
        {results.isError && <li className="px-3 py-2 text-sm text-destructive">{results.error.message}</li>}
        {results.data?.length === 0 && <li className="px-3 py-2 text-sm text-muted-foreground">Ningún proveedor coincide.</li>}
        {results.data?.map((supplier) => (
          <li key={supplier.id}>
            <button
              type="button"
              className={cn('flex w-full justify-between px-3 py-2 text-left text-sm hover:bg-muted focus-visible:bg-muted focus-visible:outline-none')}
              onClick={() => onChange({ id: supplier.id, name: supplier.name })}
            >
              <span>{supplier.name}</span>
              <span className="text-muted-foreground">NIT {supplier.taxId}</span>
            </button>
          </li>
        ))}
      </ul>
      <Button type="button" variant="outline" size="sm" className="justify-self-start" onClick={() => setCreating(true)}>
        + Nuevo proveedor
      </Button>
      <CreateSupplierDialog open={creating} onOpenChange={setCreating} onCreated={onChange} />
    </div>
  )
}
