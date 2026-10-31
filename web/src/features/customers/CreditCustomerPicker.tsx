import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { formatMoney } from '@/lib/format'
import { useDebouncedValue } from '@/lib/useDebouncedValue'
import { customerKeys, searchCustomers, type Customer } from './api'

interface Props {
  selected: Customer | null
  onSelect: (customer: Customer | null) => void
}

/**
 * Who the sale is taken on credit for. Lists only customers with credit enabled, each with what is available, so the
 * cashier sees before charging whether it fits. The server checks the limit again.
 */
export function CreditCustomerPicker({ selected, onSelect }: Props) {
  const [search, setSearch] = useState('')
  const term = useDebouncedValue(search.trim())
  const results = useQuery({
    queryKey: customerKeys.search(term),
    queryFn: ({ signal }) => searchCustomers(term, signal),
    enabled: selected === null && term.length >= 2,
  })
  const withCredit = results.data?.filter((customer) => customer.credit !== null) ?? []

  if (selected?.credit)
    return (
      <div className="flex items-center justify-between gap-2 rounded-lg border px-3 py-2 text-sm">
        <span>
          <span className="font-medium">{selected.fullName}</span>
          <span className="text-muted-foreground">
            {' '}
            · {selected.credit.isSuspended ? 'fiado suspendido' : `disponible ${formatMoney(selected.credit.available)}`}
          </span>
        </span>
        <Button variant="ghost" size="sm" onClick={() => onSelect(null)}>
          Cambiar
        </Button>
      </div>
    )

  return (
    <div className="grid gap-2">
      <Input
        aria-label="Cliente del fiado"
        placeholder="Cliente: nombre o documento…"
        autoComplete="off"
        value={search}
        onChange={(event) => setSearch(event.target.value)}
      />
      {term.length >= 2 && (
        <ul className="max-h-48 overflow-y-auto rounded-lg border" aria-label="Clientes con fiado">
          {results.isPending && <li className="px-3 py-2 text-sm text-muted-foreground">Buscando…</li>}
          {results.isError && <li className="px-3 py-2 text-sm text-destructive">{results.error.message}</li>}
          {results.isSuccess && withCredit.length === 0 && (
            <li className="px-3 py-2 text-sm text-muted-foreground">Ningún cliente con fiado coincide.</li>
          )}
          {withCredit.map((customer) => (
            <li key={customer.id}>
              <button
                type="button"
                className="flex w-full justify-between gap-3 px-3 py-2 text-left text-sm hover:bg-muted focus-visible:bg-muted focus-visible:outline-none"
                onClick={() => {
                  onSelect(customer)
                  setSearch('')
                }}
              >
                <span className="font-medium">{customer.fullName}</span>
                <span className="text-muted-foreground">
                  {customer.credit!.isSuspended ? 'suspendido' : `disponible ${formatMoney(customer.credit!.available)}`}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
