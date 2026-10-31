import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus } from 'lucide-react'
import { useState } from 'react'
import { useForm, type UseFormRegisterReturn, type UseFormSetValue } from 'react-hook-form'
import { z } from 'zod'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { createCategory, listCategories, productKeys, type Product, type ProductDetails, type SaleUnitInput } from '@/features/products/api'
import type { VatRate } from '@/features/purchases/api'
import { vatRateLabels } from '@/features/purchases/labels'

const vatRates = Object.keys(vatRateLabels) as VatRate[]

const schema = z
  .object({
    name: z.string().trim().min(1, { error: 'Escriba el nombre del producto.' }),
    baseUnitName: z.string().trim().min(1, { error: 'Escriba la unidad base (p. ej. Tableta).' }),
    activeIngredient: z.string().trim(),
    concentration: z.string().trim(),
    pharmaceuticalForm: z.string().trim(),
    sanitaryRegistration: z.string().trim(),
    requiresPrescription: z.boolean(),
    vatRate: z.enum(vatRates),
    categoryId: z.string(),
    withFirstUnit: z.boolean(),
    unitName: z.string().trim(),
    unitFactor: z.string(),
    unitPrice: z.string(),
    unitBarcode: z.string().trim(),
  })
  // The first presentation is asked only when creating: the API needs at least one.
  .superRefine((values, context) => {
    if (!values.withFirstUnit) return
    if (values.unitName === '') context.addIssue({ code: 'custom', path: ['unitName'], message: 'Escriba el nombre de la presentación.' })
    if (!Number.isInteger(Number(values.unitFactor)) || Number(values.unitFactor) < 1)
      context.addIssue({ code: 'custom', path: ['unitFactor'], message: 'Un número entero de 1 o más.' })
    if (values.unitPrice.trim() === '' || !(Number(values.unitPrice) > 0))
      context.addIssue({ code: 'custom', path: ['unitPrice'], message: 'Escriba un precio mayor que cero.' })
  })

type Values = z.infer<typeof schema>

const orNull = (value: string) => (value.trim() === '' ? null : value.trim())

interface Props {
  /** Editing: the product's current data. Creating: undefined, and the first presentation is asked too. */
  product?: Product
  submitLabel: string
  pending: boolean
  error: Error | null
  onSubmit: (details: ProductDetails, firstUnit: SaleUnitInput | null) => void
}

export function ProductForm({ product, submitLabel, pending, error, onSubmit }: Props) {
  const { register, handleSubmit, setValue, formState: { errors, isDirty } } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: product?.name ?? '',
      baseUnitName: product?.baseUnitName ?? 'Tableta',
      activeIngredient: product?.activeIngredient ?? '',
      concentration: product?.concentration ?? '',
      pharmaceuticalForm: product?.pharmaceuticalForm ?? '',
      sanitaryRegistration: product?.sanitaryRegistration ?? '',
      requiresPrescription: product?.requiresPrescription ?? false,
      vatRate: product?.vatRate ?? 'Exempt',
      categoryId: product?.categoryId ?? '',
      withFirstUnit: product === undefined,
      unitName: '',
      unitFactor: '1',
      unitPrice: '',
      unitBarcode: '',
    },
  })

  const submit = handleSubmit((values) =>
    onSubmit(
      {
        name: values.name,
        baseUnitName: values.baseUnitName,
        requiresPrescription: values.requiresPrescription,
        activeIngredient: orNull(values.activeIngredient),
        concentration: orNull(values.concentration),
        pharmaceuticalForm: orNull(values.pharmaceuticalForm),
        sanitaryRegistration: orNull(values.sanitaryRegistration),
        categoryId: orNull(values.categoryId),
        vatRate: values.vatRate,
      },
      values.withFirstUnit
        ? { name: values.unitName, conversionFactor: Number(values.unitFactor), price: Number(values.unitPrice), barcode: orNull(values.unitBarcode) }
        : null,
    ),
  )

  const field = (id: keyof Values, label: string, props: React.ComponentProps<typeof Input> = {}) => (
    <div className="grid gap-2">
      <Label htmlFor={`product-${id}`}>{label}</Label>
      <Input id={`product-${id}`} autoComplete="off" aria-invalid={errors[id] ? true : undefined} {...props} {...register(id)} />
      {errors[id] && <p className="text-sm text-destructive">{errors[id]?.message}</p>}
    </div>
  )

  return (
    <form onSubmit={submit} noValidate className="grid gap-4">
      {error && (
        <Alert variant="destructive" role="alert">
          <AlertDescription>{error.message}</AlertDescription>
        </Alert>
      )}

      <div className="grid gap-4 sm:grid-cols-[2fr_1fr]">
        {field('name', 'Nombre', { autoFocus: product === undefined, placeholder: 'Acetaminofén 500 mg' })}
        {field('baseUnitName', 'Unidad base (en la que se cuenta el stock)')}
      </div>
      <div className="grid gap-4 sm:grid-cols-3">
        {field('activeIngredient', 'Principio activo (opcional)')}
        {field('concentration', 'Concentración (opcional)')}
        {field('pharmaceuticalForm', 'Forma farmacéutica (opcional)')}
      </div>
      <div className="grid gap-4 sm:grid-cols-3">
        {field('sanitaryRegistration', 'Registro INVIMA (opcional)')}
        <div className="grid gap-2">
          <Label htmlFor="product-vatRate">IVA (incluido en el precio)</Label>
          <NativeSelect id="product-vatRate" {...register('vatRate')}>
            {vatRates.map((rate) => (
              <NativeSelectOption key={rate} value={rate}>
                {vatRateLabels[rate]}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </div>
        <CategoryField register={register('categoryId')} setValue={setValue} />
      </div>
      <label className="flex items-center gap-2 text-sm">
        <input type="checkbox" className="size-4 accent-primary" {...register('requiresPrescription')} />
        Requiere fórmula médica (el POS pedirá marcar "Fórmula revisada")
      </label>

      {product === undefined && (
        <fieldset className="grid gap-4 rounded-lg border p-4">
          <legend className="px-1 text-sm font-medium">Primera presentación</legend>
          <div className="grid gap-4 sm:grid-cols-4">
            {field('unitName', 'Nombre', { placeholder: 'Caja x30' })}
            {field('unitFactor', 'Unidades base que trae', { type: 'number', min: 1, inputMode: 'numeric' })}
            {field('unitPrice', 'Precio', { type: 'number', min: 0, step: 'any', inputMode: 'decimal' })}
            {field('unitBarcode', 'Código de barras (opcional)')}
          </div>
        </fieldset>
      )}

      <div className="flex justify-end">
        <Button type="submit" disabled={pending || (product !== undefined && !isDirty)}>
          {pending ? 'Guardando…' : submitLabel}
        </Button>
      </div>
    </form>
  )
}

/** The category list, with a way to create one without leaving the form. */
function CategoryField({ register, setValue }: { register: UseFormRegisterReturn<'categoryId'>; setValue: UseFormSetValue<Values> }) {
  const queryClient = useQueryClient()
  const categories = useQuery({ queryKey: productKeys.categories, queryFn: ({ signal }) => listCategories(signal) })
  const [creating, setCreating] = useState(false)
  const [name, setName] = useState('')
  const mutation = useMutation({
    mutationFn: () => createCategory(name),
    onSuccess: async (id) => {
      await queryClient.invalidateQueries({ queryKey: productKeys.categories })
      setValue('categoryId', id, { shouldDirty: true })
      setCreating(false)
      setName('')
    },
  })

  return (
    <div className="grid gap-2">
      <Label htmlFor="product-categoryId">Categoría (opcional)</Label>
      {creating ? (
        <div className="grid gap-1">
          <div className="flex gap-2">
            <Input aria-label="Nombre de la categoría nueva" autoFocus value={name} onChange={(event) => setName(event.target.value)} />
            <Button type="button" disabled={name.trim() === '' || mutation.isPending} onClick={() => mutation.mutate()}>
              Crear
            </Button>
          </div>
          {mutation.isError && <p className="text-sm text-destructive">{mutation.error.message}</p>}
        </div>
      ) : (
        <div className="flex gap-2">
          <NativeSelect id="product-categoryId" className="flex-1" {...register}>
            <NativeSelectOption value="">Sin categoría</NativeSelectOption>
            {categories.data?.map((category) => (
              <NativeSelectOption key={category.id} value={category.id}>
                {category.name}
              </NativeSelectOption>
            ))}
          </NativeSelect>
          <Button type="button" variant="outline" size="icon" aria-label="Nueva categoría" onClick={() => setCreating(true)}>
            <Plus />
          </Button>
        </div>
      )}
    </div>
  )
}
