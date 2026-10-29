import { describe, expect, it } from 'vitest'
import type { Sale } from '@/features/sales/api'
import { draftFrom, refundOf, returnProblem, setDisposition, setReturnQuantity, toRequest } from './returnDraft'

const item = (id: string, quantity: number, unitPrice: number, returnedQuantity = 0) => ({
  id,
  productId: `product-${id}`,
  productName: `Producto ${id}`,
  saleUnitName: 'Caja',
  quantity,
  unitPrice,
  vatRate: 'Exempt' as const,
  total: quantity * unitPrice,
  vat: 0,
  requiresPrescription: false,
  returnedQuantity,
})

const sale = (items: ReturnType<typeof item>[]): Sale => ({
  id: 'sale',
  number: 120,
  soldAt: '2026-10-27T15:00:00Z',
  cashSessionId: 'shift',
  soldByName: 'Ana',
  total: items.reduce((sum, i) => sum + i.total, 0),
  vat: 0,
  change: 0,
  prescriptionChecked: false,
  items,
  payments: [],
})

describe('return draft', () => {
  it('starts with nothing to return and shows what is left on each line', () => {
    const draft = draftFrom(sale([item('a', 3, 12_000, 1), item('b', 2, 8_000)]))

    expect(draft.map((line) => [line.remaining, line.quantity, line.disposition])).toEqual([
      [2, 0, 'Restock'],
      [2, 0, 'Restock'],
    ])
    expect(refundOf(draft)).toBe(0)
  })

  it('a quantity must be a whole number between 0 and what is left', () => {
    const draft = draftFrom(sale([item('a', 3, 12_000, 1)]))

    expect(setReturnQuantity(draft, 'a', 2)[0].quantity).toBe(2)
    for (const invalid of [3, -1, 1.5]) expect(setReturnQuantity(draft, 'a', invalid)[0].quantity).toBe(0)
  })

  it('refunds what was paid, and sends only the lines with something to return', () => {
    let draft = draftFrom(sale([item('a', 3, 12_000), item('b', 2, 8_000)]))
    draft = setReturnQuantity(draft, 'a', 1)
    draft = setDisposition(setReturnQuantity(draft, 'b', 2), 'b', 'Discard')

    expect(refundOf(draft)).toBe(28_000)
    expect(toRequest('sale', '  Cambio de fórmula ', setReturnQuantity(draft, 'b', 0))).toEqual({
      saleId: 'sale',
      reason: 'Cambio de fórmula',
      lines: [{ saleItemId: 'a', quantity: 1, disposition: 'Restock' }],
    })
  })

  it('says why it cannot be returned yet', () => {
    const fullyReturned = draftFrom(sale([item('a', 2, 12_000, 2)]))
    expect(returnProblem(fullyReturned, 'Motivo')).toContain('completo')

    const draft = draftFrom(sale([item('a', 2, 12_000)]))
    expect(returnProblem(draft, 'Motivo')).toContain('cuántas')
    expect(returnProblem(setReturnQuantity(draft, 'a', 1), '  ')).toContain('motivo')
    expect(returnProblem(setReturnQuantity(draft, 'a', 1), 'Motivo')).toBeNull()
  })
})
