import { useQuery } from '@tanstack/react-query'
import type { Product } from '@/features/products/api'
import { api } from '@/lib/api/http'

export interface LowStockAlert {
  product: Product
  /** Base units in batches that have not expired. */
  sellableUnits: number
  minimumStock: number
}

export interface BatchAlert {
  batchId: string
  product: Product
  lotNumber: string
  expirationDate: string
  quantity: number
  /** Negative once expired. */
  daysLeft: number
}

export interface StockAlerts {
  lowStock: LowStockAlert[]
  /** Expired with units: only a write-off is left. */
  expired: BatchAlert[]
  /** Expire within 90 days: sell them first, or return them to the supplier. */
  expiringSoon: BatchAlert[]
  count: number
}

// Under ['products', …]: every "stock" or "catalog" notice (real time) refreshes it.
export const alertKeys = {
  all: ['products', 'alerts'] as const,
}

export function getStockAlerts(signal?: AbortSignal): Promise<StockAlerts> {
  return api<StockAlerts>('/api/stock-alerts', { signal })
}

/** @param minimumStock In base units; null removes it. */
export function setMinimumStock(productId: string, minimumStock: number | null): Promise<void> {
  return api<void>(`/api/products/${productId}/minimum-stock`, { method: 'PUT', body: { minimumStock } })
}

/**
 * The products that just went below their minimum: in the new list and not in the previous one. Null previous =
 * the first load, which announces nothing (those alerts were already there; the page lists them).
 */
export function newlyLow(previousIds: ReadonlySet<string> | null, current: readonly LowStockAlert[]): LowStockAlert[] {
  if (previousIds === null) return []
  return current.filter((alert) => !previousIds.has(alert.product.id))
}

/** "Venció hace 3 días", "Vence hoy", "Vence en 12 días". */
export function describeDaysLeft(daysLeft: number): string {
  if (daysLeft < 0) return daysLeft === -1 ? 'Venció ayer' : `Venció hace ${-daysLeft} días`
  if (daysLeft === 0) return 'Vence hoy'
  return daysLeft === 1 ? 'Vence mañana' : `Vence en ${daysLeft} días`
}

/** The alert count for the "Alertas" link: the same query as the page, so they never disagree. */
export function useStockAlertCount(enabled: boolean): number {
  const alerts = useQuery({ queryKey: alertKeys.all, queryFn: ({ signal }) => getStockAlerts(signal), enabled })
  return alerts.data?.count ?? 0
}
