import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { toFullNit } from '@/lib/nit'
import { createSupplier } from './api'

const schema = z.object({
  name: z.string().trim().min(1, { error: 'Escriba el nombre del proveedor.' }),
  nitNumber: z
    .string()
    .refine((value) => toFullNit(value) !== null, { error: 'Escriba solo los números del NIT, sin el dígito de verificación.' }),
  contactName: z.string().trim(),
  phone: z.string().trim(),
  email: z.union([z.literal(''), z.email({ error: 'Escriba un correo válido.' })]),
})

type SupplierForm = z.infer<typeof schema>

interface Props {
  open: boolean
  onOpenChange: (open: boolean) => void
  /** Called with the new supplier, so the screen that opened the dialog can select it. */
  onCreated: (supplier: { id: string; name: string }) => void
}

export function CreateSupplierDialog({ open, onOpenChange, onCreated }: Props) {
  const queryClient = useQueryClient()
  const { register, handleSubmit, control, reset, formState: { errors } } = useForm<SupplierForm>({
    resolver: zodResolver(schema),
    defaultValues: { name: '', nitNumber: '', contactName: '', phone: '', email: '' },
  })

  // Instant feedback while typing; the API validates it again on save.
  const fullNit = toFullNit(useWatch({ control, name: 'nitNumber' }))

  const mutation = useMutation({
    mutationFn: (values: SupplierForm) =>
      createSupplier({
        name: values.name,
        taxId: toFullNit(values.nitNumber)!,
        contactName: values.contactName || undefined,
        phone: values.phone || undefined,
        email: values.email || undefined,
      }),
    onSuccess: (id, values) => {
      void queryClient.invalidateQueries({ queryKey: ['suppliers'] })
      onCreated({ id, name: values.name })
      reset()
      onOpenChange(false)
    },
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Nuevo proveedor</DialogTitle>
          <DialogDescription>El NIT se valida con su dígito de verificación (DIAN).</DialogDescription>
        </DialogHeader>

        <form id="create-supplier" onSubmit={handleSubmit((values) => mutation.mutate(values))} noValidate className="grid gap-4">
          {mutation.isError && (
            <Alert variant="destructive" role="alert">
              <AlertDescription>{mutation.error.message}</AlertDescription>
            </Alert>
          )}

          <div className="grid gap-2">
            <Label htmlFor="supplier-name">Nombre</Label>
            <Input id="supplier-name" autoFocus aria-invalid={errors.name ? true : undefined} {...register('name')} />
            {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
          </div>

          <div className="grid gap-2">
            <Label htmlFor="supplier-nit">NIT (sin dígito de verificación)</Label>
            <div className="flex items-center gap-2">
              <Input id="supplier-nit" inputMode="numeric" placeholder="800987654" aria-invalid={errors.nitNumber ? true : undefined} {...register('nitNumber')} />
              <span className="w-28 shrink-0 text-sm text-muted-foreground" aria-live="polite">
                {fullNit ? <>→ <strong className="text-foreground">{fullNit}</strong></> : ''}
              </span>
            </div>
            {errors.nitNumber && <p className="text-sm text-destructive">{errors.nitNumber.message}</p>}
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="grid gap-2">
              <Label htmlFor="supplier-contact">Contacto (opcional)</Label>
              <Input id="supplier-contact" {...register('contactName')} />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="supplier-phone">Teléfono (opcional)</Label>
              <Input id="supplier-phone" inputMode="tel" {...register('phone')} />
            </div>
          </div>

          <div className="grid gap-2">
            <Label htmlFor="supplier-email">Correo (opcional)</Label>
            <Input id="supplier-email" type="email" aria-invalid={errors.email ? true : undefined} {...register('email')} />
            {errors.email && <p className="text-sm text-destructive">{errors.email.message}</p>}
          </div>
        </form>

        <DialogFooter>
          <Button type="submit" form="create-supplier" disabled={mutation.isPending}>
            {mutation.isPending ? 'Guardando…' : 'Crear proveedor'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
