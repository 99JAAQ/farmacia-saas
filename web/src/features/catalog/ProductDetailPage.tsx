import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowLeft } from 'lucide-react'
import { Link, useParams } from 'react-router'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button, buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { can } from '@/features/auth/permissions'
import { useAuth } from '@/features/auth/useAuth'
import { KardexTable } from '@/features/inventory/KardexTable'
import { getProduct, productKeys, setProductActive, updateProduct, type Product } from '@/features/products/api'
import { cn } from '@/lib/utils'
import { BatchesTable } from './BatchesTable'
import { ProductForm } from './ProductForm'
import { SaleUnitsEditor } from './SaleUnitsEditor'

export function ProductDetailPage() {
  const { id = '' } = useParams()
  const product = useQuery({ queryKey: productKeys.detail(id), queryFn: ({ signal }) => getProduct(id, signal) })

  return (
    <div className="grid gap-6">
      <Link to="/catalogo" className={cn(buttonVariants({ variant: 'ghost', size: 'sm' }), 'w-fit')}>
        <ArrowLeft />
        Catálogo
      </Link>
      {product.isPending && <p className="text-sm text-muted-foreground">Cargando…</p>}
      {product.isError && <p className="text-sm text-destructive">{product.error.message}</p>}
      {product.data && <ProductView product={product.data} />}
    </div>
  )
}

/** The product's own data, not its presentations: what the details form edits. */
const detailsKey = (p: Product) =>
  JSON.stringify([
    p.name, p.baseUnitName, p.requiresPrescription, p.activeIngredient, p.concentration,
    p.pharmaceuticalForm, p.sanitaryRegistration, p.categoryId, p.vatRate,
  ])

function ProductView({ product }: { product: Product }) {
  const { user } = useAuth()
  const managesInventory = can(user, 'manageInventory')
  const queryClient = useQueryClient()
  const onSuccess = () => void queryClient.invalidateQueries({ queryKey: productKeys.all })
  const save = useMutation({ mutationFn: (details: Parameters<typeof updateProduct>[1]) => updateProduct(product.id, details), onSuccess })
  const toggle = useMutation({ mutationFn: () => setProductActive(product.id, !product.isActive), onSuccess })

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-2xl font-semibold">{product.name}</h1>
          {!product.isActive && <Badge variant="outline">Descontinuado</Badge>}
        </div>
        <Button variant="outline" disabled={toggle.isPending} onClick={() => toggle.mutate()}>
          {product.isActive ? 'Descontinuar' : 'Reactivar'}
        </Button>
      </div>
      {!product.isActive && (
        <Alert>
          <AlertDescription>
            Descontinuado: no aparece en el POS y no se vende ni se compra. Sus ventas, lotes y movimientos se conservan.
          </AlertDescription>
        </Alert>
      )}
      {toggle.isError && <p className="text-sm text-destructive">{toggle.error.message}</p>}

      <Card>
        <CardHeader>
          <CardTitle>Datos</CardTitle>
        </CardHeader>
        <CardContent>
          {/* A new key only when the saved details change (not a presentation): unsaved edits here are kept. */}
          <ProductForm key={detailsKey(product)} product={product} submitLabel="Guardar cambios" pending={save.isPending} error={save.error} onSubmit={(details) => save.mutate(details)} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Presentaciones</CardTitle>
          <CardDescription>Un precio nuevo aplica a las ventas siguientes: los tiquetes anteriores conservan el suyo.</CardDescription>
        </CardHeader>
        <CardContent>
          <SaleUnitsEditor product={product} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Lotes</CardTitle>
          <CardDescription>El que vence primero es el que el POS vende primero (FEFO).</CardDescription>
        </CardHeader>
        <CardContent>
          <BatchesTable product={product} />
        </CardContent>
      </Card>

      {managesInventory && (
        <Card>
          <CardHeader>
            <CardTitle>Movimientos</CardTitle>
            <CardDescription>Cada entrada y salida de stock, la más reciente primero, con quién la hizo.</CardDescription>
          </CardHeader>
          <CardContent>
            <KardexTable productId={product.id} baseUnitName={product.baseUnitName} />
          </CardContent>
        </Card>
      )}
    </>
  )
}
