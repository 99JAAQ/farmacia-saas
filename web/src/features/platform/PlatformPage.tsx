import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { toFullNit } from '@/lib/nit'
import { passwordField, passwordHint } from '@/lib/password'
import { createPharmacy, listPharmacies, platformKeys } from './api'

/** The platform administrator's screen: the pharmacies on the platform and their owners. */
export function PlatformPage() {
  const pharmacies = useQuery({ queryKey: platformKeys.pharmacies, queryFn: ({ signal }) => listPharmacies(signal) })
  const [creating, setCreating] = useState(false)

  return (
    <div className="grid gap-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Farmacias</h1>
          <p className="text-muted-foreground">Las farmacias de la plataforma y quién las administra.</p>
        </div>
        <Button onClick={() => setCreating(true)}>
          <Plus aria-hidden="true" />
          Nueva farmacia
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>{pharmacies.data ? `${pharmacies.data.length} ${pharmacies.data.length === 1 ? 'farmacia' : 'farmacias'}` : 'Farmacias'}</CardTitle>
          <CardDescription>Desde aquí no se ven ventas, inventario ni clientes: esos datos son de cada farmacia.</CardDescription>
        </CardHeader>
        <CardContent>
          {pharmacies.isPending && <p className="text-sm text-muted-foreground">Cargando…</p>}
          {pharmacies.isError && <p className="text-sm text-destructive">{pharmacies.error.message}</p>}
          {pharmacies.data && (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Farmacia</TableHead>
                  <TableHead>NIT</TableHead>
                  <TableHead>Dueño</TableHead>
                  <TableHead className="text-right">Usuarios</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {pharmacies.data.map((pharmacy) => (
                  <TableRow key={pharmacy.id}>
                    <TableCell>
                      <div className="font-medium">{pharmacy.name}</div>
                      {pharmacy.address && <div className="text-sm text-muted-foreground">{pharmacy.address}</div>}
                    </TableCell>
                    <TableCell className="tabular-nums">{pharmacy.taxId}</TableCell>
                    <TableCell>
                      {pharmacy.owners.length === 0 ? (
                        <span className="text-destructive">Sin dueño</span>
                      ) : (
                        pharmacy.owners.map((owner) => (
                          <div key={owner.email}>
                            {owner.fullName} <span className="text-sm text-muted-foreground">{owner.email}</span>
                          </div>
                        ))
                      )}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">{pharmacy.userCount}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {creating && <CreatePharmacyDialog onClose={() => setCreating(false)} />}
    </div>
  )
}

const schema = z.object({
  name: z.string().trim().min(1, { error: 'Escriba el nombre de la farmacia.' }),
  nitNumber: z.string().refine((value) => toFullNit(value) !== null, { error: 'Escriba solo los números del NIT, sin el dígito de verificación.' }),
  address: z.string().trim(),
  phone: z.string().trim(),
  isVatResponsible: z.boolean(),
  ownerFullName: z.string().trim().min(1, { error: 'Escriba el nombre del dueño.' }),
  ownerEmail: z.email({ error: 'Escriba un correo válido: con él inicia sesión el dueño.' }),
  ownerPassword: passwordField,
})

type Values = z.infer<typeof schema>

const orNull = (value: string) => (value === '' ? null : value)

function CreatePharmacyDialog({ onClose }: { onClose: () => void }) {
  const queryClient = useQueryClient()
  const { register, handleSubmit, control, formState: { errors } } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: '', nitNumber: '', address: '', phone: '', isVatResponsible: true, ownerFullName: '', ownerEmail: '', ownerPassword: '',
    },
  })
  // The verification digit, live; the API validates it again.
  const fullNit = toFullNit(useWatch({ control, name: 'nitNumber' }))

  const mutation = useMutation({
    mutationFn: (values: Values) =>
      createPharmacy({
        name: values.name,
        taxId: toFullNit(values.nitNumber)!,
        address: orNull(values.address),
        phone: orNull(values.phone),
        isVatResponsible: values.isVatResponsible,
        ownerFullName: values.ownerFullName,
        ownerEmail: values.ownerEmail.trim(),
        ownerPassword: values.ownerPassword,
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: platformKeys.pharmacies })
      onClose()
    },
  })

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Nueva farmacia</DialogTitle>
          <DialogDescription>Se crea con su dueño, que después crea a sus cajeros y bodegueros.</DialogDescription>
        </DialogHeader>
        <form id="create-pharmacy" className="grid gap-4" noValidate onSubmit={handleSubmit((values) => mutation.mutate(values))}>
          {mutation.isError && (
            <Alert variant="destructive" role="alert">
              <AlertDescription>{mutation.error.message}</AlertDescription>
            </Alert>
          )}
          <div className="grid gap-2">
            <Label htmlFor="pharmacy-name">Nombre de la farmacia</Label>
            <Input id="pharmacy-name" autoFocus aria-invalid={errors.name ? true : undefined} {...register('name')} />
            {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
          </div>
          <div className="grid gap-2">
            <Label htmlFor="pharmacy-nit">NIT (sin dígito de verificación)</Label>
            <div className="flex items-center gap-2">
              <Input id="pharmacy-nit" inputMode="numeric" placeholder="900123456" aria-invalid={errors.nitNumber ? true : undefined} {...register('nitNumber')} />
              <span className="w-28 shrink-0 text-sm text-muted-foreground" aria-live="polite">
                {fullNit ? <>→ <strong className="text-foreground">{fullNit}</strong></> : ''}
              </span>
            </div>
            {errors.nitNumber && <p className="text-sm text-destructive">{errors.nitNumber.message}</p>}
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="grid gap-2">
              <Label htmlFor="pharmacy-address">Dirección (opcional)</Label>
              <Input id="pharmacy-address" {...register('address')} />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="pharmacy-phone">Teléfono (opcional)</Label>
              <Input id="pharmacy-phone" inputMode="tel" {...register('phone')} />
            </div>
          </div>
          <label className="flex items-center gap-3 text-sm">
            <input type="checkbox" className="size-4 accent-primary" {...register('isVatResponsible')} />
            Responsable de IVA (el dueño puede cambiarlo después)
          </label>

          <fieldset className="grid gap-4 rounded-lg border p-4">
            <legend className="px-1 text-sm font-medium">Dueño</legend>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="grid gap-2">
                <Label htmlFor="owner-name">Nombre</Label>
                <Input id="owner-name" aria-invalid={errors.ownerFullName ? true : undefined} {...register('ownerFullName')} />
                {errors.ownerFullName && <p className="text-sm text-destructive">{errors.ownerFullName.message}</p>}
              </div>
              <div className="grid gap-2">
                <Label htmlFor="owner-email">Correo</Label>
                <Input id="owner-email" type="email" autoComplete="off" aria-invalid={errors.ownerEmail ? true : undefined} {...register('ownerEmail')} />
                {errors.ownerEmail && <p className="text-sm text-destructive">{errors.ownerEmail.message}</p>}
              </div>
            </div>
            <div className="grid gap-2">
              <Label htmlFor="owner-password">Contraseña temporal</Label>
              {/* Visible on purpose: the administrator gives it to the owner, who must change it at the first login. */}
              <Input id="owner-password" type="text" autoComplete="off" spellCheck={false} aria-describedby="owner-password-hint"
                aria-invalid={errors.ownerPassword ? true : undefined} {...register('ownerPassword')} />
              <p id="owner-password-hint" className={errors.ownerPassword ? 'text-sm text-destructive' : 'text-sm text-muted-foreground'}>
                {errors.ownerPassword?.message ?? `${passwordHint} El dueño la cambia al entrar.`}
              </p>
            </div>
          </fieldset>
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>Cancelar</Button>
          <Button type="submit" form="create-pharmacy" disabled={mutation.isPending}>
            {mutation.isPending ? 'Creando…' : 'Crear farmacia'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
