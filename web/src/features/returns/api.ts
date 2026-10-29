import { api } from '@/lib/api/http'

export type ReturnDisposition = 'Restock' | 'Discard'

export interface SaleReturnItem {
  id: string
  saleItemId: string
  productName: string
  saleUnitName: string
  quantity: number
  unitPrice: number
  refund: number
  disposition: ReturnDisposition
}

/** The return voucher. */
export interface SaleReturn {
  id: string
  number: number
  saleId: string
  saleNumber: number
  returnedAt: string
  cashSessionId: string
  returnedByName: string
  reason: string
  /** Cash given back. */
  refund: number
  items: SaleReturnItem[]
}

export interface RegisterSaleReturnRequest {
  saleId: string
  reason: string
  /** Quantity in the ticket line's sale unit. */
  lines: { saleItemId: string; quantity: number; disposition: ReturnDisposition }[]
}

/** Answers with the whole voucher, ready to print. */
export function registerSaleReturn(request: RegisterSaleReturnRequest): Promise<SaleReturn> {
  return api<SaleReturn>('/api/sale-returns', { method: 'POST', body: request })
}
