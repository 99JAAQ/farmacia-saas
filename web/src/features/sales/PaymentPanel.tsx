import { Plus, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { formatMoney } from '@/lib/format'
import { cn } from '@/lib/utils'
import { nonCashMethods, paymentMethodLabels } from './labels'
import type { NonCashMethod, NonCashRow, PaymentSummary } from './payment'

interface Props {
  summary: PaymentSummary
  cashText: string
  onCashText: (text: string) => void
  rows: NonCashRow[]
  onRows: (rows: NonCashRow[]) => void
}

export function PaymentPanel({ summary, cashText, onCashText, rows, onRows }: Props) {
  const available = nonCashMethods.filter((method) => !rows.some((row) => row.method === method))
  const setRow = (method: NonCashMethod, text: string) =>
    onRows(rows.map((row) => (row.method === method ? { ...row, text } : row)))

  return (
    <div className="grid gap-4">
      {rows.map((row) => (
        <div key={row.method} className="grid gap-1.5">
          <Label htmlFor={`pay-${row.method}`}>{paymentMethodLabels[row.method]}</Label>
          <div className="flex gap-2">
            <Input
              id={`pay-${row.method}`}
              type="number"
              min={0}
              step="any"
              inputMode="decimal"
              className="tabular-nums"
              value={row.text}
              onChange={(event) => setRow(row.method, event.target.value)}
            />
            <Button
              variant="ghost"
              size="icon"
              aria-label={`Quitar el pago con ${paymentMethodLabels[row.method]}`}
              onClick={() => onRows(rows.filter((other) => other.method !== row.method))}
            >
              <X />
            </Button>
          </div>
        </div>
      ))}

      {available.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {available.map((method) => (
            <Button
              key={method}
              variant="outline"
              size="sm"
              // By default it pays what is still due: usually the whole sale goes by Nequi or card.
              onClick={() => onRows([...rows, { method, text: summary.cashDue > 0 ? String(summary.cashDue) : '' }])}
            >
              <Plus />
              {paymentMethodLabels[method]}
            </Button>
          ))}
        </div>
      )}

      {summary.cashDue > 0 && (
        <div className="grid gap-1.5">
          <div className="flex items-end justify-between">
            <Label htmlFor="cashReceived">Efectivo recibido</Label>
            {summary.nonCashTotal > 0 && (
              <span className="text-xs text-muted-foreground">Por cubrir: {formatMoney(summary.cashDue)}</span>
            )}
          </div>
          <div className="flex gap-2">
            <Input
              id="cashReceived"
              type="number"
              min={0}
              step="any"
              inputMode="decimal"
              className="h-11 text-lg tabular-nums"
              value={cashText}
              onChange={(event) => onCashText(event.target.value)}
            />
            <Button variant="outline" className="h-11" onClick={() => onCashText(String(summary.cashDue))}>
              Exacto
            </Button>
          </div>
        </div>
      )}

      <div
        role="status"
        className={cn(
          'flex items-baseline justify-between rounded-lg px-3 py-2',
          summary.missing > 0 ? 'bg-destructive/10 text-destructive' : 'bg-success/10 text-success',
        )}
      >
        <span className="text-sm font-medium">{summary.missing > 0 ? 'Falta' : 'Devuelta'}</span>
        <span className="text-2xl font-semibold tabular-nums">
          {formatMoney(summary.missing > 0 ? summary.missing : summary.change)}
        </span>
      </div>
    </div>
  )
}
