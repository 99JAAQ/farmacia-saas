import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { useNavigate } from 'react-router'
import { z } from 'zod'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { SupplierPicker } from '@/features/suppliers/SupplierPicker'
import { todayLocal } from '@/lib/format'
import { createPurchaseDraft, purchaseKeys } from './api'
import { paymentTermLabels } from './labels'

// The same rules the domain enforces, checked here only to show them next to the field before sending.
const schema = z
  .object({
    supplier: z
      .object({ id: z.string(), name: z.string() })
      .nullable()
      .refine((supplier) => supplier !== null, { error: 'Elija el proveedor.' }),
    invoiceNumber: z.string().trim().min(1, { error: 'Escriba el número de la factura.' }),
    invoiceDate: z.string().min(1, { error: 'Escriba la fecha de la factura.' }),
    paymentTerm: z.enum(['Cash', 'Credit']),
    dueDate: z.string(),
  })
  .superRefine((values, ctx) => {
    if (values.invoiceDate > todayLocal())
      ctx.addIssue({ code: 'custom', path: ['invoiceDate'], message: 'La fecha de la factura no puede ser futura.' })

    if (values.paymentTerm === 'Credit' && !values.dueDate)
      ctx.addIssue({ code: 'custom', path: ['dueDate'], message: 'Una compra a crédito necesita la fecha de pago.' })
    else if (values.paymentTerm === 'Credit' && values.dueDate < values.invoiceDate)
      ctx.addIssue({ code: 'custom', path: ['dueDate'], message: 'El pago no puede vencer antes de la factura.' })
  })

// What the form holds while typing (supplier may still be null) vs. what it delivers once valid (it cannot).
type HeaderInput = z.input<typeof schema>
type HeaderForm = z.output<typeof schema>

export function NewPurchasePage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const { register, control, handleSubmit, formState: { errors } } = useForm<HeaderInput, unknown, HeaderForm>({
    resolver: zodResolver(schema),
    defaultValues: { supplier: null, invoiceNumber: '', invoiceDate: todayLocal(), paymentTerm: 'Cash', dueDate: '' },
  })

  const mutation = useMutation({
    mutationFn: (values: HeaderForm) =>
      createPurchaseDraft({
        supplierId: values.supplier.id,
        invoiceNumber: values.invoiceNumber,
        invoiceDate: values.invoiceDate,
        paymentTerm: values.paymentTerm,
        dueDate: values.paymentTerm === 'Credit' ? values.dueDate : null,
      }),
    onSuccess: (id) => {
      void queryClient.invalidateQueries({ queryKey: purchaseKeys.all })
      navigate(`/compras/${id}`, { replace: true })
    },
  })

  const isCredit = useWatch({ control, name: 'paymentTerm' }) === 'Credit'

  return (
    <Card className="max-w-2xl">
      <CardHeader>
        <CardTitle className="text-xl">Nueva compra</CardTitle>
        <CardDescription>Datos de la factura. Las líneas se agregan en el siguiente paso y se guardan una a una.</CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit((values) => mutation.mutate(values))} noValidate className="grid gap-5">
          {mutation.isError && (
            <Alert variant="destructive" role="alert">
              <AlertDescription>{mutation.error.message}</AlertDescription>
            </Alert>
          )}

          <div className="grid gap-2">
            <Label htmlFor="supplier">Proveedor</Label>
            <Controller
              control={control}
              name="supplier"
              render={({ field, fieldState }) => (
                <SupplierPicker id="supplier" value={field.value} onChange={field.onChange} invalid={fieldState.invalid} />
              )}
            />
            {errors.supplier && <p className="text-sm text-destructive">{errors.supplier.message}</p>}
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="grid gap-2">
              <Label htmlFor="invoiceNumber">Número de factura</Label>
              <Input id="invoiceNumber" aria-invalid={errors.invoiceNumber ? true : undefined} {...register('invoiceNumber')} />
              {errors.invoiceNumber && <p className="text-sm text-destructive">{errors.invoiceNumber.message}</p>}
            </div>
            <div className="grid gap-2">
              <Label htmlFor="invoiceDate">Fecha de la factura</Label>
              <Input id="invoiceDate" type="date" max={todayLocal()} aria-invalid={errors.invoiceDate ? true : undefined} {...register('invoiceDate')} />
              {errors.invoiceDate && <p className="text-sm text-destructive">{errors.invoiceDate.message}</p>}
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="grid gap-2">
              <Label htmlFor="paymentTerm">Forma de pago</Label>
              <NativeSelect id="paymentTerm" {...register('paymentTerm')}>
                <NativeSelectOption value="Cash">{paymentTermLabels.Cash}</NativeSelectOption>
                <NativeSelectOption value="Credit">{paymentTermLabels.Credit}</NativeSelectOption>
              </NativeSelect>
            </div>
            {isCredit && (
              <div className="grid gap-2">
                <Label htmlFor="dueDate">Fecha de pago</Label>
                <Input id="dueDate" type="date" aria-invalid={errors.dueDate ? true : undefined} {...register('dueDate')} />
                {errors.dueDate && <p className="text-sm text-destructive">{errors.dueDate.message}</p>}
              </div>
            )}
          </div>

          <Button type="submit" size="lg" className="justify-self-start" disabled={mutation.isPending}>
            {mutation.isPending ? 'Creando…' : 'Crear borrador'}
          </Button>
        </form>
      </CardContent>
    </Card>
  )
}
