import { Card, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

/** Placeholder for the catalog (phase 2). Exists now to exercise the role-protected route. */
export function CatalogPage() {
  return (
    <Card className="max-w-xl">
      <CardHeader>
        <CardTitle className="text-xl">Catálogo</CardTitle>
        <CardDescription>
          Aquí se crearán y editarán los productos (fase 2). Solo el dueño y el bodeguero ven esta sección.
        </CardDescription>
      </CardHeader>
    </Card>
  )
}
