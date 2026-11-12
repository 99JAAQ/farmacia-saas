import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { pharmacyKeys, updatePharmacy, type Pharmacy } from './api'
import { PharmacyHeader } from './PharmacyHeader'
import { usePharmacy } from './usePharmacy'

const schema = z.object({
  name: z.string().trim().min(1, { error: 'Escriba el nombre de la farmacia.' }),
  address: z.string().trim(),
  phone: z.string().trim(),
  isVatResponsible: z.boolean(),
})

type Values = z.infer<typeof schema>

const orNull = (value: string) => (value === '' ? null : value)

/** The owner's settings: how the pharmacy appears on tickets, and whether purchase VAT is deductible. */
export function PharmacyPage() {
  const pharmacy = usePharmacy()

  return (
    <div className="grid gap-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Mi farmacia</h1>
        <p className="text-muted-foreground">Los datos que se imprimen en los tiquetes y comprobantes.</p>
      </div>
      {pharmacy.isPending && <p className="text-sm text-muted-foreground">Cargando…</p>}
      {pharmacy.isError && <p className="text-sm text-destructive">{pharmacy.error.message}</p>}
      {/* Keyed by the saved data: after saving, the form starts again from what the server stored. */}
      {pharmacy.data && <PharmacyForm key={JSON.stringify(pharmacy.data)} pharmacy={pharmacy.data} />}
    </div>
  )
}

function PharmacyForm({ pharmacy }: { pharmacy: Pharmacy }) {
  const queryClient = useQueryClient()
  const { register, handleSubmit, formState: { errors, isDirty } } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: pharmacy.name,
      address: pharmacy.address ?? '',
      phone: pharmacy.phone ?? '',
      isVatResponsible: pharmacy.isVatResponsible,
    },
  })

  const mutation = useMutation({
    mutationFn: (values: Values) =>
      updatePharmacy({ name: values.name, address: orNull(values.address), phone: orNull(values.phone), isVatResponsible: values.isVatResponsible }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: pharmacyKeys.current }),
  })

  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_20rem] lg:items-start">
      <Card>
        <CardHeader>
          <CardTitle>Datos de la farmacia</CardTitle>
          <CardDescription>El NIT no se cambia aquí: es con el que se registró la farmacia.</CardDescription>
        </CardHeader>
        <CardContent>
          <form className="grid gap-4" onSubmit={handleSubmit((values) => mutation.mutate(values))} noValidate>
            {mutation.isError && (
              <Alert variant="destructive" role="alert">
                <AlertDescription>{mutation.error.message}</AlertDescription>
              </Alert>
            )}
            {mutation.isSuccess && !isDirty && (
              <Alert role="status">
                <AlertDescription>Guardado. Los próximos tiquetes salen con estos datos.</AlertDescription>
              </Alert>
            )}
            <div className="grid gap-2">
              <Label htmlFor="pharmacy-name">Nombre</Label>
              <Input id="pharmacy-name" aria-invalid={errors.name !== undefined} {...register('name')} />
              {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
            </div>
            <div className="grid gap-2">
              <Label htmlFor="pharmacy-nit">NIT</Label>
              <Input id="pharmacy-nit" value={pharmacy.taxId} readOnly disabled />
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <div className="grid gap-2">
                <Label htmlFor="pharmacy-address">Dirección</Label>
                <Input id="pharmacy-address" autoComplete="off" {...register('address')} />
              </div>
              <div className="grid gap-2">
                <Label htmlFor="pharmacy-phone">Teléfono</Label>
                <Input id="pharmacy-phone" inputMode="tel" autoComplete="off" {...register('phone')} />
              </div>
            </div>
            <label className="flex items-start gap-3 rounded-lg border p-3">
              <input type="checkbox" className="mt-0.5 size-4 accent-primary" {...register('isVatResponsible')} />
              <span className="grid gap-1 text-sm">
                <span className="font-medium">Responsable de IVA</span>
                <span className="text-muted-foreground">
                  El IVA de las compras se descuenta y no va al costo de los productos. Confírmelo con su contador. El
                  cambio aplica a las compras que se confirmen desde ahora.
                </span>
              </span>
            </label>
            <div>
              <Button type="submit" disabled={!isDirty || mutation.isPending}>
                {mutation.isPending ? 'Guardando…' : 'Guardar'}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Así se ve en el tiquete</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="grid gap-0.5 rounded-md border border-dashed p-4 text-center font-mono text-sm">
            <PharmacyHeader />
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
