import { useQuery } from '@tanstack/react-query'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { getCurrentUser } from '@/features/auth/api'
import { useAuth } from '@/features/auth/useAuth'

/** Temporary home: greets the user and checks that the API accepts the access token. */
export function HomePage() {
  const { user } = useAuth()
  const me = useQuery({
    queryKey: ['auth', 'me'],
    queryFn: ({ signal }) => getCurrentUser(signal),
  })

  return (
    <Card className="max-w-xl">
      <CardHeader>
        <CardTitle className="text-xl">Hola, {user?.fullName}</CardTitle>
        <CardDescription>Las ventas, el inventario y la caja llegan en la fase 2.</CardDescription>
      </CardHeader>
      <CardContent>
        <p className="text-sm">
          Verificación con la API (<code>/api/auth/me</code>):{' '}
          {me.isPending && 'consultando…'}
          {me.isError && <span className="text-destructive">{me.error.message}</span>}
          {me.isSuccess && <span className="font-medium text-green-700">token aceptado</span>}
        </p>
      </CardContent>
    </Card>
  )
}
