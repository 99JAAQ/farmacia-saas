import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate } from 'react-router'
import { z } from 'zod'
import { Logo } from '@/components/Logo'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button, buttonVariants } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { ApiError } from '@/lib/api/http'
import { passwordField, passwordHint } from '@/lib/password'
import { changePassword } from './api'
import { useAuth } from './useAuth'

const schema = z
  .object({
    currentPassword: z.string().min(1, { error: 'Escriba su contraseña actual.' }),
    newPassword: passwordField,
    confirmation: z.string(),
  })
  .refine((values) => values.newPassword === values.confirmation, { path: ['confirmation'], error: 'No coincide con la nueva.' })
  .refine((values) => values.newPassword !== values.currentPassword, { path: ['newPassword'], error: 'Debe ser distinta de la actual.' })

type Values = z.infer<typeof schema>

/**
 * Full screen, like the login: a user with a temporary password lands here before anything else (RequireAuth),
 * and the rest of the app would answer 403 anyway. Also reachable from the menu to change it voluntarily.
 */
export function ChangePasswordPage() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [serverError, setServerError] = useState<string | null>(null)
  const forced = user?.mustChangePassword ?? false

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { currentPassword: '', newPassword: '', confirmation: '' },
  })

  const onSubmit = handleSubmit(async (values) => {
    setServerError(null)
    try {
      await changePassword(values.currentPassword, values.newPassword)
      navigate('/', { replace: true })
    } catch (error) {
      setServerError(error instanceof ApiError ? error.message : 'Ocurrió un error inesperado. Intente de nuevo.')
    }
  })

  return (
    <main className="flex min-h-svh items-center justify-center bg-muted p-4">
      <Card className="w-full max-w-sm">
        <CardHeader className="gap-3">
          <Logo />
          <div className="grid gap-1">
            <CardTitle className="text-xl">{forced ? 'Cree su contraseña' : 'Cambiar contraseña'}</CardTitle>
            <CardDescription>
              {forced
                ? `Hola, ${user?.fullName}. Entró con una contraseña temporal: cámbiela por una que solo usted conozca.`
                : 'Se cerrará su sesión en los demás equipos.'}
            </CardDescription>
          </div>
        </CardHeader>
        <CardContent>
          <form onSubmit={onSubmit} noValidate className="grid gap-4">
            {serverError && (
              <Alert variant="destructive" role="alert">
                <AlertDescription>{serverError}</AlertDescription>
              </Alert>
            )}

            <div className="grid gap-2">
              <Label htmlFor="current-password">{forced ? 'Contraseña temporal' : 'Contraseña actual'}</Label>
              <Input id="current-password" type="password" autoComplete="current-password" autoFocus
                aria-invalid={errors.currentPassword ? true : undefined} {...register('currentPassword')} />
              {errors.currentPassword && <p className="text-sm text-destructive">{errors.currentPassword.message}</p>}
            </div>

            <div className="grid gap-2">
              <Label htmlFor="new-password">Contraseña nueva</Label>
              <Input id="new-password" type="password" autoComplete="new-password" aria-describedby="new-password-hint"
                aria-invalid={errors.newPassword ? true : undefined} {...register('newPassword')} />
              <p id="new-password-hint" className={errors.newPassword ? 'text-sm text-destructive' : 'text-sm text-muted-foreground'}>
                {errors.newPassword?.message ?? passwordHint}
              </p>
            </div>

            <div className="grid gap-2">
              <Label htmlFor="confirm-password">Repita la nueva</Label>
              <Input id="confirm-password" type="password" autoComplete="new-password"
                aria-invalid={errors.confirmation ? true : undefined} {...register('confirmation')} />
              {errors.confirmation && <p className="text-sm text-destructive">{errors.confirmation.message}</p>}
            </div>

            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Guardando…' : 'Guardar contraseña'}
            </Button>
            {forced ? (
              <Button type="button" variant="ghost" onClick={() => void logout()}>
                Salir
              </Button>
            ) : (
              <Link to="/" className={buttonVariants({ variant: 'ghost' })}>
                Volver
              </Link>
            )}
          </form>
        </CardContent>
      </Card>
    </main>
  )
}
