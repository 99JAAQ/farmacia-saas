import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { useForm, type UseFormRegisterReturn } from 'react-hook-form'
import { z } from 'zod'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { roleLabels } from '@/features/auth/roles'
import { useAuth } from '@/features/auth/useAuth'
import { passwordField, passwordHint } from '@/lib/password'
import {
  changeUserRole, createUser, describeStatus, listUsers, pharmacyRoles, resetUserPassword, setUserActive, userKeys,
  type PharmacyRole, type UserAccount,
} from './api'

const toneClasses = {
  success: 'bg-success/10 text-success',
  warning: 'bg-warning/10 text-warning',
  muted: 'bg-muted text-muted-foreground',
} as const

/** The owner's view of who works at the pharmacy. */
export function UsersPage() {
  const users = useQuery({ queryKey: userKeys.all, queryFn: ({ signal }) => listUsers(signal) })
  const [creating, setCreating] = useState(false)
  const [resetting, setResetting] = useState<UserAccount | null>(null)
  const [deactivating, setDeactivating] = useState<UserAccount | null>(null)

  return (
    <div className="grid gap-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Usuarios</h1>
          <p className="text-muted-foreground">Quién trabaja en la farmacia y qué puede hacer.</p>
        </div>
        <Button onClick={() => setCreating(true)}>
          <Plus aria-hidden="true" />
          Nuevo usuario
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Equipo</CardTitle>
          <CardDescription>
            Cada usuario entra con su correo. Si alguien se va, desactívelo: nunca se borra, porque sus ventas y
            movimientos siguen diciendo quién los hizo.
          </CardDescription>
        </CardHeader>
        <CardContent>
          {users.isPending && <p className="text-sm text-muted-foreground">Cargando…</p>}
          {users.isError && <p className="text-sm text-destructive">{users.error.message}</p>}
          {users.data && (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Nombre</TableHead>
                  <TableHead>Rol</TableHead>
                  <TableHead>Estado</TableHead>
                  <TableHead className="w-0" />
                </TableRow>
              </TableHeader>
              <TableBody>
                {users.data.map((user) => (
                  <UserRow key={`${user.id}-${user.role}`} user={user} onReset={setResetting} onDeactivate={setDeactivating} />
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {/* Mounted only while open: each opening starts empty. */}
      {creating && <CreateUserDialog onClose={() => setCreating(false)} />}
      {resetting && <ResetPasswordDialog user={resetting} onClose={() => setResetting(null)} />}
      {deactivating && <DeactivateDialog user={deactivating} onClose={() => setDeactivating(null)} />}
    </div>
  )
}

function UserRow({ user, onReset, onDeactivate }: {
  user: UserAccount
  onReset: (user: UserAccount) => void
  onDeactivate: (user: UserAccount) => void
}) {
  const { user: me } = useAuth()
  const queryClient = useQueryClient()
  const isMe = user.id === me?.id
  const status = describeStatus(user)
  const refresh = () => queryClient.invalidateQueries({ queryKey: userKeys.all })

  const role = useMutation({ mutationFn: (newRole: PharmacyRole) => changeUserRole(user.id, newRole), onSettled: refresh })
  const activate = useMutation({ mutationFn: () => setUserActive(user.id, true), onSuccess: refresh })
  const error = role.error ?? activate.error

  return (
    <TableRow className={user.isActive ? undefined : 'text-muted-foreground'}>
      <TableCell>
        <div className="font-medium">{user.fullName}{isMe && <span className="font-normal text-muted-foreground"> (usted)</span>}</div>
        <div className="text-sm text-muted-foreground">{user.email}</div>
        {error && <p role="alert" className="mt-1 text-sm text-destructive">{error.message}</p>}
      </TableCell>
      <TableCell>
        {/* The owner cannot change their own role (the API refuses it): another owner has to. */}
        {isMe || !user.isActive ? (
          roleLabels[user.role]
        ) : (
          <NativeSelect
            aria-label={`Rol de ${user.fullName}`}
            value={user.role}
            disabled={role.isPending}
            onChange={(event) => role.mutate(event.target.value as PharmacyRole)}
          >
            {pharmacyRoles.map((option) => (
              <NativeSelectOption key={option} value={option}>
                {roleLabels[option]}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        )}
      </TableCell>
      <TableCell>
        <Badge className={toneClasses[status.tone]}>{status.text}</Badge>
      </TableCell>
      <TableCell>
        {!isMe && (
          <div className="flex justify-end gap-2">
            {user.isActive ? (
              <>
                <Button variant="outline" size="sm" onClick={() => onReset(user)}>
                  Restablecer contraseña
                </Button>
                <Button variant="outline" size="sm" onClick={() => onDeactivate(user)}>
                  Desactivar
                </Button>
              </>
            ) : (
              <Button variant="outline" size="sm" disabled={activate.isPending} onClick={() => activate.mutate()}>
                Reactivar
              </Button>
            )}
          </div>
        )}
      </TableCell>
    </TableRow>
  )
}

const newUserSchema = z.object({
  fullName: z.string().trim().min(1, { error: 'Escriba el nombre.' }),
  email: z.email({ error: 'Escriba un correo válido: con él inicia sesión.' }),
  role: z.enum(pharmacyRoles),
  temporaryPassword: passwordField,
})

type NewUserValues = z.infer<typeof newUserSchema>

function CreateUserDialog({ onClose }: { onClose: () => void }) {
  const queryClient = useQueryClient()
  const { register, handleSubmit, formState: { errors } } = useForm<NewUserValues>({
    resolver: zodResolver(newUserSchema),
    defaultValues: { fullName: '', email: '', role: 'Cashier', temporaryPassword: '' },
  })
  const mutation = useMutation({
    mutationFn: (values: NewUserValues) => createUser({ ...values, email: values.email.trim() }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: userKeys.all })
      onClose()
    },
  })

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Nuevo usuario</DialogTitle>
          <DialogDescription>
            Dígale la contraseña temporal en persona. Al entrar por primera vez, la app le pedirá cambiarla.
          </DialogDescription>
        </DialogHeader>
        <form id="create-user" className="grid gap-4" noValidate onSubmit={handleSubmit((values) => mutation.mutate(values))}>
          {mutation.isError && (
            <Alert variant="destructive" role="alert">
              <AlertDescription>{mutation.error.message}</AlertDescription>
            </Alert>
          )}
          <div className="grid gap-2">
            <Label htmlFor="user-name">Nombre</Label>
            <Input id="user-name" autoFocus aria-invalid={errors.fullName ? true : undefined} {...register('fullName')} />
            {errors.fullName && <p className="text-sm text-destructive">{errors.fullName.message}</p>}
          </div>
          <div className="grid gap-2">
            <Label htmlFor="user-email">Correo</Label>
            <Input id="user-email" type="email" autoComplete="off" aria-invalid={errors.email ? true : undefined} {...register('email')} />
            {errors.email && <p className="text-sm text-destructive">{errors.email.message}</p>}
          </div>
          <div className="grid gap-2">
            <Label htmlFor="user-role">Rol</Label>
            <NativeSelect id="user-role" {...register('role')}>
              {pharmacyRoles.map((option) => (
                <NativeSelectOption key={option} value={option}>
                  {roleLabels[option]}
                </NativeSelectOption>
              ))}
            </NativeSelect>
          </div>
          <TemporaryPasswordField error={errors.temporaryPassword?.message} registration={register('temporaryPassword')} />
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancelar</Button>
          <Button type="submit" form="create-user" disabled={mutation.isPending}>
            {mutation.isPending ? 'Creando…' : 'Crear usuario'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

const resetSchema = z.object({ temporaryPassword: passwordField })

function ResetPasswordDialog({ user, onClose }: { user: UserAccount; onClose: () => void }) {
  const queryClient = useQueryClient()
  const { register, handleSubmit, formState: { errors } } = useForm<z.infer<typeof resetSchema>>({
    resolver: zodResolver(resetSchema),
    defaultValues: { temporaryPassword: '' },
  })
  const mutation = useMutation({
    mutationFn: (password: string) => resetUserPassword(user.id, password),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: userKeys.all })
      onClose()
    },
  })

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Restablecer la contraseña de {user.fullName}</DialogTitle>
          <DialogDescription>
            Se cerrarán sus sesiones abiertas. Entrará con esta contraseña temporal y la app le pedirá cambiarla.
          </DialogDescription>
        </DialogHeader>
        <form id="reset-password" className="grid gap-4" noValidate
          onSubmit={handleSubmit((values) => mutation.mutate(values.temporaryPassword))}>
          {mutation.isError && (
            <Alert variant="destructive" role="alert">
              <AlertDescription>{mutation.error.message}</AlertDescription>
            </Alert>
          )}
          <TemporaryPasswordField autoFocus error={errors.temporaryPassword?.message} registration={register('temporaryPassword')} />
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancelar</Button>
          <Button type="submit" form="reset-password" disabled={mutation.isPending}>
            {mutation.isPending ? 'Guardando…' : 'Restablecer'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function DeactivateDialog({ user, onClose }: { user: UserAccount; onClose: () => void }) {
  const queryClient = useQueryClient()
  const mutation = useMutation({
    mutationFn: () => setUserActive(user.id, false),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: userKeys.all })
      onClose()
    },
  })

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>¿Desactivar a {user.fullName}?</DialogTitle>
          <DialogDescription>
            No podrá volver a entrar, y su sesión abierta se cierra en máximo 15 minutos. Sus ventas y movimientos se
            conservan. Puede reactivarlo cuando quiera.
          </DialogDescription>
        </DialogHeader>
        {mutation.isError && (
          <Alert variant="destructive" role="alert">
            <AlertDescription>{mutation.error.message}</AlertDescription>
          </Alert>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancelar</Button>
          <Button variant="destructive" disabled={mutation.isPending} onClick={() => mutation.mutate()}>
            {mutation.isPending ? 'Desactivando…' : 'Desactivar'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function TemporaryPasswordField({ error, registration, autoFocus = false }: {
  error: string | undefined
  registration: UseFormRegisterReturn
  autoFocus?: boolean
}) {
  return (
    <div className="grid gap-2">
      <Label htmlFor="temporary-password">Contraseña temporal</Label>
      {/* Visible on purpose: the owner reads it out to the user. */}
      <Input id="temporary-password" type="text" autoComplete="off" spellCheck={false} autoFocus={autoFocus}
        aria-describedby="temporary-password-hint" aria-invalid={error ? true : undefined} {...registration} />
      <p id="temporary-password-hint" className={error ? 'text-sm text-destructive' : 'text-sm text-muted-foreground'}>
        {error ?? passwordHint}
      </p>
    </div>
  )
}
