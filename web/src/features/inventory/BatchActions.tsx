import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { pluralize } from '@/features/catalog/stock'
import { productKeys, type ProductBatch } from '@/features/products/api'
import { cn } from '@/lib/utils'
import { adjustBatch, writeOffBatch } from './api'
import { describeCount, formatChange } from './labels'

interface Props {
  batch: ProductBatch
  baseUnitName: string
}

/** "Dar de baja" (expired batches only) and "Ajustar" for one batch. Both leave who and why in the kardex. */
export function BatchActions({ batch, baseUnitName }: Props) {
  const [dialog, setDialog] = useState<'writeOff' | 'adjust' | null>(null)

  return (
    <div className="flex justify-end gap-1">
      {batch.isExpired && (
        <Button size="sm" variant="destructive" onClick={() => setDialog('writeOff')}>
          Dar de baja
        </Button>
      )}
      <Button size="sm" variant="outline" onClick={() => setDialog('adjust')}>
        Ajustar
      </Button>
      {/* Mounted only while open: each opening starts clean. */}
      {dialog === 'writeOff' && <WriteOffDialog batch={batch} baseUnitName={baseUnitName} onClose={() => setDialog(null)} />}
      {dialog === 'adjust' && <AdjustDialog batch={batch} baseUnitName={baseUnitName} onClose={() => setDialog(null)} />}
    </div>
  )
}

/** The product's stock, batches, kardex and catalog line all change: they all live under the products key. */
function useInventoryMutation(run: () => Promise<void>, onClose: () => void) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: run,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: productKeys.all })
      onClose()
    },
  })
}

function WriteOffDialog({ batch, baseUnitName, onClose }: Props & { onClose: () => void }) {
  const mutation = useInventoryMutation(() => writeOffBatch(batch.id), onClose)

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Dar de baja el lote {batch.lotNumber}</DialogTitle>
          <DialogDescription>
            El lote venció: saca sus {batch.quantity} {pluralize(baseUnitName, batch.quantity)} del inventario. Queda en el
            kardex con su nombre; no se puede deshacer.
          </DialogDescription>
        </DialogHeader>
        {mutation.isError && (
          <Alert variant="destructive" role="alert">
            <AlertDescription>{mutation.error.message}</AlertDescription>
          </Alert>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancelar
          </Button>
          <Button variant="destructive" disabled={mutation.isPending} onClick={() => mutation.mutate()}>
            {mutation.isPending ? 'Dando de baja…' : 'Dar de baja'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function AdjustDialog({ batch, baseUnitName, onClose }: Props & { onClose: () => void }) {
  const [countedText, setCountedText] = useState('')
  const [reason, setReason] = useState('')
  const { difference, problem } = describeCount(batch.quantity, countedText)
  const blocked = problem ?? (reason.trim() === '' ? 'Escriba el motivo del ajuste.' : null)
  const mutation = useInventoryMutation(() => adjustBatch(batch.id, Number(countedText), reason.trim()), onClose)

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Ajustar el lote {batch.lotNumber}</DialogTitle>
          <DialogDescription>
            El sistema tiene {batch.quantity} {pluralize(baseUnitName, batch.quantity)}. Escriba cuántas contó en el estante.
          </DialogDescription>
        </DialogHeader>
        <form
          id="adjust-batch"
          className="grid gap-4"
          onSubmit={(event) => {
            event.preventDefault()
            if (blocked === null) mutation.mutate()
          }}
        >
          {mutation.isError && (
            <Alert variant="destructive" role="alert">
              <AlertDescription>{mutation.error.message}</AlertDescription>
            </Alert>
          )}
          <div className="grid gap-2">
            <Label htmlFor="counted">Contadas ({pluralize(baseUnitName, 2)})</Label>
            <div className="flex items-center gap-3">
              <Input
                id="counted"
                type="number"
                min={0}
                inputMode="numeric"
                autoFocus
                className="w-32 tabular-nums"
                value={countedText}
                onChange={(event) => {
                  mutation.reset()
                  setCountedText(event.target.value)
                }}
              />
              {difference !== null && difference !== 0 && (
                <span
                  aria-live="polite"
                  className={cn('font-semibold tabular-nums', difference < 0 ? 'text-destructive' : 'text-success')}
                >
                  {formatChange(difference)}
                </span>
              )}
            </div>
          </div>
          <div className="grid gap-2">
            <Label htmlFor="reason">Motivo</Label>
            <Input
              id="reason"
              maxLength={500}
              placeholder="Por ejemplo: dos tabletas dañadas"
              autoComplete="off"
              value={reason}
              onChange={(event) => {
                mutation.reset()
                setReason(event.target.value)
              }}
            />
          </div>
          {blocked !== null && countedText.trim() !== '' && <p className="text-sm text-muted-foreground">{blocked}</p>}
        </form>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit" form="adjust-batch" disabled={blocked !== null || mutation.isPending}>
            {mutation.isPending ? 'Ajustando…' : 'Ajustar'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
