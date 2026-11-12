import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { paymentMethodLabels } from '@/features/sales/labels'
import { formatMoney, formatTime } from '@/lib/format'
import { chartHours, dashboardKeys, getDailySummary, type DailySummary } from './api'

/**
 * The owner's view of the day. Live: the query sits under ['sales'], so every sale, return or cash notice from the
 * hub refreshes it. Numbers, not decoration: one hero figure, three tiles, one chart, two lists.
 */
export function DailySummaryPanel() {
  const summary = useQuery({ queryKey: dashboardKeys.today, queryFn: ({ signal }) => getDailySummary(signal) })

  if (summary.isPending) return <p className="text-sm text-muted-foreground">Cargando el resumen del día…</p>
  if (summary.isError) return <p className="text-sm text-destructive">{summary.error.message}</p>

  const day = summary.data
  return (
    <section aria-labelledby="today-title" className="grid gap-4">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 id="today-title" className="text-lg font-semibold">Hoy</h2>
        <p className="text-xs text-muted-foreground" aria-live="polite">
          En vivo · actualizado {formatTime(new Date(summary.dataUpdatedAt).toISOString())}
        </p>
      </div>

      <div className="grid gap-4 lg:grid-cols-4">
        <Card className="lg:col-span-1">
          <CardContent className="grid gap-1">
            <span className="text-sm text-muted-foreground">Ventas netas</span>
            {/* The one hero figure: proportional digits read better this big. */}
            <span className="text-5xl font-semibold tracking-tight">{formatMoney(day.netSales)}</span>
            <span className="text-sm text-muted-foreground">IVA incluido</span>
          </CardContent>
        </Card>
        <StatTile
          label="Ventas"
          value={String(day.salesCount)}
          detail={day.salesCount > 0 ? `Ticket promedio ${formatMoney(day.averageTicket)}` : 'Todavía no hay ventas hoy'}
        />
        <StatTile
          label="Margen bruto"
          value={formatMoney(day.grossProfit)}
          detail={
            day.marginPercent === null
              ? 'Sin ventas para comparar'
              : `${day.marginPercent.toLocaleString('es-CO')} % · costo de lo vendido ${formatMoney(day.cost)}`
          }
        />
        <StatTile
          label="Devoluciones"
          value={formatMoney(day.returns)}
          detail={day.returns > 0 ? 'Ya descontadas de las ventas netas' : 'Ninguna hoy'}
        />
      </div>

      <div className="grid gap-4 lg:grid-cols-[2fr_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>Ventas por hora</CardTitle>
            <CardDescription>Total vendido en cada hora, IVA incluido.</CardDescription>
          </CardHeader>
          <CardContent>
            <HourlyChart byHour={day.byHour} />
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>Medios de pago</CardTitle>
            <CardDescription>Lo que se cobró con cada uno (el efectivo, sin la devuelta).</CardDescription>
          </CardHeader>
          <CardContent>
            <PaymentBars byPaymentMethod={day.byPaymentMethod} />
          </CardContent>
        </Card>
      </div>

      {day.topProducts.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle>Lo más vendido</CardTitle>
            <CardDescription>Por valor. Las unidades están en la unidad base (tabletas, no cajas).</CardDescription>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Producto</TableHead>
                  <TableHead className="text-right">Unidades</TableHead>
                  <TableHead className="text-right">Total</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {day.topProducts.map((product) => (
                  <TableRow key={product.productId}>
                    <TableCell className="font-medium">{product.name}</TableCell>
                    <TableCell className="text-right tabular-nums">{product.baseUnits.toLocaleString('es-CO')}</TableCell>
                    <TableCell className="text-right tabular-nums">{formatMoney(product.total)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>
      )}
    </section>
  )
}

function StatTile({ label, value, detail }: { label: string; value: string; detail: string }) {
  return (
    <Card>
      <CardContent className="grid gap-1">
        <span className="text-sm text-muted-foreground">{label}</span>
        <span className="text-3xl font-semibold tracking-tight">{value}</span>
        <span className="text-sm text-muted-foreground">{detail}</span>
      </CardContent>
    </Card>
  )
}

const hourLabel = (hour: number) => `${String(hour).padStart(2, '0')}:00`

/**
 * One series, so no legend: the title names it. Columns at most 24px wide, 4px rounded tops, square on a hairline
 * baseline; only the busiest hour carries its value; every column has a tooltip (hover or keyboard focus), and the
 * same numbers are available as a table.
 */
function HourlyChart({ byHour }: { byHour: DailySummary['byHour'] }) {
  const [active, setActive] = useState<number | null>(null)
  const hours = chartHours(byHour)
  const max = Math.max(...hours.map((h) => h.total))
  const peak = max > 0 ? hours.find((h) => h.total === max)?.hour : undefined

  if (max === 0) return <p className="text-sm text-muted-foreground">Las ventas aparecerán aquí a medida que se hagan.</p>

  return (
    <div className="grid gap-2">
      <div className="relative flex h-44 items-end border-b border-border" role="list" aria-label="Ventas por hora">
        {hours.map((h) => {
          const height = (h.total / max) * 100
          const isActive = active === h.hour
          return (
            <div
              key={h.hour}
              role="listitem"
              tabIndex={0}
              aria-label={`${hourLabel(h.hour)}: ${formatMoney(h.total)}, ${h.sales} ${h.sales === 1 ? 'venta' : 'ventas'}`}
              className="group relative flex h-full flex-1 items-end justify-center outline-none focus-visible:bg-accent/40"
              onMouseEnter={() => setActive(h.hour)}
              onMouseLeave={() => setActive(null)}
              onFocus={() => setActive(h.hour)}
              onBlur={() => setActive(null)}
            >
              {h.total > 0 && (
                <div
                  className={`w-full max-w-6 rounded-t-[4px] bg-chart-1 transition-opacity ${active !== null && !isActive ? 'opacity-40' : ''}`}
                  style={{ height: `${height}%` }}
                />
              )}
              {h.hour === peak && active === null && (
                <span className="absolute text-xs font-medium tabular-nums" style={{ bottom: `calc(${height}% + 4px)` }}>
                  {formatMoney(h.total)}
                </span>
              )}
              {isActive && (
                <div className="pointer-events-none absolute bottom-full z-10 mb-1 rounded-md border bg-popover px-2.5 py-1.5 text-xs whitespace-nowrap text-popover-foreground shadow-md">
                  <div className="font-medium">{hourLabel(h.hour)} – {hourLabel(h.hour + 1)}</div>
                  <div className="tabular-nums">{formatMoney(h.total)} · {h.sales} {h.sales === 1 ? 'venta' : 'ventas'}</div>
                </div>
              )}
            </div>
          )
        })}
      </div>
      <div className="flex" aria-hidden="true">
        {hours.map((h) => (
          <span key={h.hour} className="flex-1 text-center text-[11px] text-muted-foreground tabular-nums">
            {h.hour % 2 === 1 ? '' : h.hour}
          </span>
        ))}
      </div>
      <details className="text-sm">
        <summary className="cursor-pointer text-muted-foreground">Ver como tabla</summary>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Hora</TableHead>
              <TableHead className="text-right">Ventas</TableHead>
              <TableHead className="text-right">Total</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {byHour.map((h) => (
              <TableRow key={h.hour}>
                <TableCell className="tabular-nums">{hourLabel(h.hour)}</TableCell>
                <TableCell className="text-right tabular-nums">{h.sales}</TableCell>
                <TableCell className="text-right tabular-nums">{formatMoney(h.total)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </details>
    </div>
  )
}

/** Magnitude per method: one hue (identity is in the text), bars square at the baseline, the value at the tip. */
function PaymentBars({ byPaymentMethod }: { byPaymentMethod: DailySummary['byPaymentMethod'] }) {
  if (byPaymentMethod.length === 0) return <p className="text-sm text-muted-foreground">Sin cobros todavía.</p>

  const max = Math.max(...byPaymentMethod.map((p) => p.amount))
  return (
    <ul className="grid gap-3">
      {byPaymentMethod.map((payment) => (
        <li key={payment.method} className="grid gap-1">
          <div className="flex justify-between gap-2 text-sm">
            <span>{paymentMethodLabels[payment.method]}</span>
            <span className="font-medium tabular-nums">{formatMoney(payment.amount)}</span>
          </div>
          <div className="h-2.5" aria-hidden="true">
            <div className="h-full rounded-r-[4px] bg-chart-1" style={{ width: `${Math.max(2, (payment.amount / max) * 100)}%` }} />
          </div>
        </li>
      ))}
    </ul>
  )
}
