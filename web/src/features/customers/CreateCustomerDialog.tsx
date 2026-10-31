import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { toFullNit } from '@/lib/nit'
import { createCustomer, customerKeys, type DocumentType } from './api'
import { documentTypeLabels } from './labels'

const documentTypes = Object.keys(documentTypeLabels) as DocumentType[]

const schema = z
  .object({
    fullName: z.string().trim().min(1, { error: 'Escriba el nombre del cliente.' }),
    documentType: z.enum(documentTypes),
    documentNumber: z.string().trim().min(1, { error: 'Escriba el número de documento.' }),
    phone: z.string().trim(),
    email: z.union([z.literal(''), z.email({ error: 'Escriba un correo válido.' })]),
  })
  .refine((values) => values.documentType !== 'Nit' || toFullNit(values.documentNumber) !== null, {
    path: ['documentNumber'],
    error: 'Escriba solo los números del NIT, sin el dígito de verificación.',
  })

type CustomerForm = z.infer<typeof schema>

interface Props {
  open: boolean
  onOpenChange: (open: boolean) => void
  onCreated: (customerId: string) => void
}

/** The owner registers a customer. The API checks the document again and rejects a repeated one (409). */
export function CreateCustomerDialog({ open, onOpenChange, onCreated }: Props) {
  const queryClient = useQueryClient()
  const { register, handleSubmit, control, reset, formState: { errors } } = useForm<CustomerForm>({
    resolver: zodResolver(schema),
    defaultValues: { fullName: '', documentType: 'CitizenshipCard', documentNumber: '', phone: '', email: '' },
  })

  // Hooks always run, in the same order: never call useWatch inside a condition.
  const documentType = useWatch({ control, name: 'documentType' })
  const documentNumber = useWatch({ control, name: 'documentNumber' })
  const fullNit = documentType === 'Nit' ? toFullNit(documentNumber) : null

  const mutation = useMutation({
    mutationFn: (values: CustomerForm) =>
      createCustomer({
        fullName: values.fullName,
        documentType: values.documentType,
        documentNumber: values.documentType === 'Nit' ? toFullNit(values.documentNumber)! : values.documentNumber,
        phone: values.phone || undefined,
        email: values.email || undefined,
      }),
    onSuccess: (id) => {
      void queryClient.invalidateQueries({ queryKey: customerKeys.all })
      reset()
      onOpenChange(false)
      onCreated(id)
    },
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Nuevo cliente</DialogTitle>
          <DialogDescription>Para fiarle, después de crearlo habilite el fiado con un cupo.</DialogDescription>
        </DialogHeader>

        <form id="create-customer" onSubmit={handleSubmit((values) => mutation.mutate(values))} noValidate className="grid gap-4">
          {mutation.isError && (
            <Alert variant="destructive" role="alert">
              <AlertDescription>{mutation.error.message}</AlertDescription>
            </Alert>
          )}

          <div className="grid gap-2">
            <Label htmlFor="customer-name">Nombre</Label>
            <Input id="customer-name" autoFocus aria-invalid={errors.fullName ? true : undefined} {...register('fullName')} />
            {errors.fullName && <p className="text-sm text-destructive">{errors.fullName.message}</p>}
          </div>

          <div className="grid gap-4 sm:grid-cols-[auto_1fr]">
            <div className="grid gap-2">
              <Label htmlFor="customer-document-type">Tipo de documento</Label>
              <NativeSelect id="customer-document-type" {...register('documentType')}>
                {documentTypes.map((type) => (
                  <NativeSelectOption key={type} value={type}>
                    {documentTypeLabels[type]}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
            </div>
            <div className="grid gap-2">
              <Label htmlFor="customer-document">
                {documentType === 'Nit' ? 'NIT (sin dígito de verificación)' : 'Número de documento'}
              </Label>
              <div className="flex items-center gap-2">
                <Input
                  id="customer-document"
                  inputMode={documentType === 'Passport' ? 'text' : 'numeric'}
                  aria-invalid={errors.documentNumber ? true : undefined}
                  {...register('documentNumber')}
                />
                {fullNit && (
                  <span className="shrink-0 text-sm text-muted-foreground" aria-live="polite">
                    → <strong className="text-foreground">{fullNit}</strong>
                  </span>
                )}
              </div>
              {errors.documentNumber && <p className="text-sm text-destructive">{errors.documentNumber.message}</p>}
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="grid gap-2">
              <Label htmlFor="customer-phone">Teléfono (opcional)</Label>
              <Input id="customer-phone" inputMode="tel" {...register('phone')} />
            </div>
            <div className="grid gap-2">
              <Label htmlFor="customer-email">Correo (opcional)</Label>
              <Input id="customer-email" type="email" aria-invalid={errors.email ? true : undefined} {...register('email')} />
              {errors.email && <p className="text-sm text-destructive">{errors.email.message}</p>}
            </div>
          </div>
        </form>

        <DialogFooter>
          <Button type="submit" form="create-customer" disabled={mutation.isPending}>
            {mutation.isPending ? 'Guardando…' : 'Crear cliente'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
