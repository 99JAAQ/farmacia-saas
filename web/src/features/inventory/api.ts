import { api } from '@/lib/api/http'

export type StockMovementType = 'Purchase' | 'Sale' | 'Adjustment' | 'ExpiredWriteOff' | 'CustomerReturn'

export interface KardexEntry {
  id: string
  occurredAt: string
  type: StockMovementType
  lotNumber: string
  /** In base units: positive in, negative out. */
  quantityChange: number
  /** All the product's units right after the movement, in every batch. */
  productBalanceAfter: number
  batchBalanceAfter: number
  /** "Venta N.º 120", "Compra FE-77", "Devolución N.º 5"; null for write-offs and adjustments. */
  document: string | null
  /** An adjustment's reason. */
  notes: string | null
  /** Null in movements recorded before the kardex had users. */
  performedByName: string | null
}

/** Under the product's key: every product change (a write-off, a count) refreshes it too. */
export const kardexKey = (productId: string) => ['products', productId, 'kardex'] as const

/** Newest first. */
export function getKardex(productId: string, signal?: AbortSignal): Promise<KardexEntry[]> {
  return api<KardexEntry[]>(`/api/products/${productId}/kardex`, { signal })
}

/** All the units of an expired batch leave the inventory. */
export function writeOffBatch(batchId: string): Promise<void> {
  return api<void>(`/api/batches/${batchId}/write-off`, { method: 'POST' })
}

/** The batch is left at what was counted on the shelf, in base units. */
export function adjustBatch(batchId: string, countedQuantity: number, reason: string): Promise<void> {
  return api<void>(`/api/batches/${batchId}/adjust`, { method: 'POST', body: { countedQuantity, reason } })
}
