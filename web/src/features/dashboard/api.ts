import type { PaymentMethod } from '@/features/sales/api'
import { api } from '@/lib/api/http'

/** How the day is going (Colombia's day). Owner only: it shows costs and margin. */
export interface DailySummary {
  date: string
  salesCount: number
  grossSales: number
  returns: number
  /** Sold minus refunded today, VAT included: what came in. */
  netSales: number
  averageTicket: number
  vat: number
  cost: number
  /** Net sales without VAT, minus what the units cost. */
  grossProfit: number
  /** Of the net sales without VAT; null when there are none. */
  marginPercent: number | null
  byPaymentMethod: { method: PaymentMethod; amount: number; payments: number }[]
  byHour: { hour: number; total: number; sales: number }[]
  topProducts: { productId: string; name: string; baseUnits: number; total: number }[]
}

// Under ['sales', …]: every "sales" or "cash" notice (real time) refreshes it, with no code of its own.
export const dashboardKeys = {
  today: ['sales', 'summary', 'today'] as const,
}

export function getDailySummary(signal?: AbortSignal): Promise<DailySummary> {
  return api<DailySummary>('/api/dashboard/today', { signal })
}

/**
 * The hours the chart shows: the usual opening hours (7 to 19), widened to include any hour that had sales, with
 * the hours without sales at zero so the gaps show.
 */
export function chartHours(byHour: DailySummary['byHour']): { hour: number; total: number; sales: number }[] {
  const hours = byHour.map((h) => h.hour)
  const first = Math.min(7, ...hours)
  const last = Math.max(19, ...hours)
  return Array.from({ length: last - first + 1 }, (_, i) => {
    const hour = first + i
    return byHour.find((h) => h.hour === hour) ?? { hour, total: 0, sales: 0 }
  })
}
