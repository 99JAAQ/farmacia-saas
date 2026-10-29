import type { Sale } from '@/features/sales/api'
import type { RegisterSaleReturnRequest, ReturnDisposition } from './api'

/** What the cashier is about to return from one ticket line. */
export interface DraftLine {
  saleItemId: string
  productName: string
  saleUnitName: string
  unitPrice: number
  /** Sold minus already returned: the most this line can return now. */
  remaining: number
  quantity: number
  disposition: ReturnDisposition
}

/** Every line starts at 0 to return and "back to stock": the cashier picks what the customer brought. */
export function draftFrom(sale: Sale): DraftLine[] {
  return sale.items.map((item) => ({
    saleItemId: item.id,
    productName: item.productName,
    saleUnitName: item.saleUnitName,
    unitPrice: item.unitPrice,
    remaining: item.quantity - item.returnedQuantity,
    quantity: 0,
    disposition: 'Restock',
  }))
}

/** Whole units between 0 and what is left on the line (the server checks it again). */
export function setReturnQuantity(draft: DraftLine[], saleItemId: string, quantity: number): DraftLine[] {
  return draft.map((line) =>
    line.saleItemId === saleItemId && Number.isInteger(quantity) && quantity >= 0 && quantity <= line.remaining
      ? { ...line, quantity }
      : line,
  )
}

export function setDisposition(draft: DraftLine[], saleItemId: string, disposition: ReturnDisposition): DraftLine[] {
  return draft.map((line) => (line.saleItemId === saleItemId ? { ...line, disposition } : line))
}

/** Cash to give back: what was paid for those units (the server computes the same from the ticket). */
export function refundOf(draft: DraftLine[]): number {
  return draft.reduce((sum, line) => sum + line.quantity * line.unitPrice, 0)
}

/** Why it cannot be returned yet; null when it can. */
export function returnProblem(draft: DraftLine[], reason: string): string | null {
  if (draft.every((line) => line.remaining === 0)) return 'Este tiquete ya se devolvió completo.'
  if (draft.every((line) => line.quantity === 0)) return 'Indique cuántas unidades devuelve.'
  if (reason.trim() === '') return 'Escriba el motivo de la devolución.'
  return null
}

/** Only the lines with something to return. */
export function toRequest(saleId: string, reason: string, draft: DraftLine[]): RegisterSaleReturnRequest {
  return {
    saleId,
    reason: reason.trim(),
    lines: draft
      .filter((line) => line.quantity > 0)
      .map(({ saleItemId, quantity, disposition }) => ({ saleItemId, quantity, disposition })),
  }
}
