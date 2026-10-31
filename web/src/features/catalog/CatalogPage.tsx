import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { Button, buttonVariants } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { createProduct, getCatalog, productKeys } from '@/features/products/api'
import { formatDate, formatMoney, todayLocal } from '@/lib/format'
import { useDebouncedValue } from '@/lib/useDebouncedValue'
import { ProductForm } from './ProductForm'
import { daysUntil, expiringSoonDays, formatStock, pluralize } from './stock'

/** The catalog: products with their prices and stock, and what needs attention (expired or soon to expire). */
export function CatalogPage() {
  const [search, setSearch] = useState('')
  const [includeInactive, setIncludeInactive] = useState(false)
  const [creating, setCreating] = useState(false)
  const term = useDebouncedValue(search.trim())
  const catalog = useQuery({
    queryKey: productKeys.catalog(term, includeInactive),
    queryFn: ({ signal }) => getCatalog(term, includeInactive, signal),
  })
  const today = todayLocal()

  return (
    <div className="grid gap-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold">Catálogo</h1>
        <Button onClick={() => setCreating(true)}>
          <Plus />
          Nuevo producto
        </Button>
      </div>

      <div className="flex flex-wrap items-center gap-4">
        <Input
          aria-label="Buscar producto"
          placeholder="Nombre, principio activo o código de barras…"
          className="max-w-md"
          autoComplete="off"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" className="size-4 accent-primary" checked={includeInactive} onChange={(event) => setIncludeInactive(event.target.checked)} />
          Mostrar descontinuados
        </label>
      </div>

      {catalog.isPending && <p className="text-sm text-muted-foreground">Cargando…</p>}
      {catalog.isError && <p className="text-sm text-destructive">{catalog.error.message}</p>}
      {catalog.data?.length === 0 && <p className="text-sm text-muted-foreground">Ningún producto coincide.</p>}
      {catalog.data && catalog.data.length > 0 && (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Producto</TableHead>
              <TableHead>Presentaciones</TableHead>
              <TableHead>Stock</TableHead>
              <TableHead>Próximo vencimiento</TableHead>
              <TableHead className="w-0" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {catalog.data.map(({ product, sellableUnits, expiredUnits, nextExpiration }) => {
              const days = nextExpiration ? daysUntil(nextExpiration, today) : null
              return (
                <TableRow key={product.id} className={product.isActive ? undefined : 'opacity-60'}>
                  <TableCell>
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-medium">{product.name}</span>
                      {!product.isActive && <Badge variant="outline">Descontinuado</Badge>}
                      {product.requiresPrescription && <Badge variant="outline">Fórmula</Badge>}
                    </div>
                    {product.activeIngredient && <div className="text-sm text-muted-foreground">{product.activeIngredient}</div>}
                  </TableCell>
                  <TableCell className="text-sm">
                    {product.saleUnits.map((unit) => (
                      <div key={unit.id}>
                        {unit.name} · <span className="tabular-nums">{formatMoney(unit.price)}</span>
                      </div>
                    ))}
                  </TableCell>
                  <TableCell>
                    <div>{formatStock(sellableUnits, product.saleUnits, product.baseUnitName)}</div>
                    {expiredUnits > 0 && (
                      <Badge className="mt-1 bg-destructive/10 text-destructive">
                        {expiredUnits} {pluralize(product.baseUnitName, expiredUnits)} en lotes vencidos
                      </Badge>
                    )}
                  </TableCell>
                  <TableCell>
                    {nextExpiration ? (
                      <span className="flex flex-wrap items-center gap-2">
                        {formatDate(nextExpiration)}
                        {days !== null && days <= expiringSoonDays && <Badge className="bg-warning/10 text-warning">En {days} días</Badge>}
                      </span>
                    ) : (
                      '—'
                    )}
                  </TableCell>
                  <TableCell>
                    <Link to={`/catalogo/${product.id}`} className={buttonVariants({ variant: 'outline', size: 'sm' })}>
                      Ver
                    </Link>
                  </TableCell>
                </TableRow>
              )
            })}
          </TableBody>
        </Table>
      )}

      <NewProductDialog open={creating} onOpenChange={setCreating} />
    </div>
  )
}

/** Data and the first presentation; the other presentations are added in the product's page. */
function NewProductDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const mutation = useMutation({
    mutationFn: ({ details, unit }: { details: Parameters<typeof createProduct>[0]; unit: Parameters<typeof createProduct>[1] }) =>
      createProduct(details, unit),
    onSuccess: (id) => {
      void queryClient.invalidateQueries({ queryKey: productKeys.all })
      onOpenChange(false)
      void navigate(`/catalogo/${id}`)
    },
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>Nuevo producto</DialogTitle>
          <DialogDescription>El stock se cuenta en la unidad base; cada presentación dice cuántas trae y su precio.</DialogDescription>
        </DialogHeader>
        {/* Mounted only while open: the form starts empty each time. */}
        {open && (
          <ProductForm
            submitLabel="Crear producto"
            pending={mutation.isPending}
            error={mutation.error}
            onSubmit={(details, unit) => unit && mutation.mutate({ details, unit })}
          />
        )}
      </DialogContent>
    </Dialog>
  )
}
