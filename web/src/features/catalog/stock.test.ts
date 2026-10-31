import { describe, expect, it } from 'vitest'
import type { SaleUnit } from '@/features/products/api'
import { daysUntil, formatStock, pluralize } from './stock'

const unit = (name: string, conversionFactor: number): SaleUnit => ({ id: name, name, conversionFactor, price: 1, barcode: null })
const boxBlisterTablet = [unit('Caja x30', 30), unit('Blíster x10', 10), unit('Tableta', 1)]

describe('formatStock', () => {
  it('expresses the stock in the largest presentation and what is left loose', () => {
    expect(formatStock(95, boxBlisterTablet, 'Tableta')).toBe('3 × Caja x30 + 5 tabletas')
    expect(formatStock(90, boxBlisterTablet, 'Tableta')).toBe('3 × Caja x30')
    expect(formatStock(29, boxBlisterTablet, 'Tableta')).toBe('29 tabletas')
    expect(formatStock(31, boxBlisterTablet, 'Tableta')).toBe('1 × Caja x30 + 1 tableta')
  })

  it('without a presentation bigger than one unit, or without stock', () => {
    expect(formatStock(12, [unit('Frasco', 1)], 'Frasco')).toBe('12 frascos')
    expect(formatStock(0, boxBlisterTablet, 'Tableta')).toBe('Sin stock')
  })

  it('does not depend on the order of the presentations', () => {
    expect(formatStock(95, [...boxBlisterTablet].reverse(), 'Tableta')).toBe('3 × Caja x30 + 5 tabletas')
  })
})

describe('pluralize and daysUntil', () => {
  it('makes the Spanish plural of pharmacy units', () => {
    expect([pluralize('Cápsula', 2), pluralize('Unidad', 3), pluralize('Sobre', 1)]).toEqual(['cápsulas', 'unidades', 'sobre'])
  })

  it('counts calendar days between two dates, across months and leap years', () => {
    expect(daysUntil('2026-11-30', '2026-10-31')).toBe(30)
    expect(daysUntil('2028-03-01', '2028-02-28')).toBe(2) // 2028 is a leap year
    expect(daysUntil('2026-10-30', '2026-10-31')).toBe(-1)
  })
})
