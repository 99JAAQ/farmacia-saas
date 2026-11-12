import { describe, expect, it } from 'vitest'
import type { Product } from '@/features/products/api'
import { describeDaysLeft, newlyLow, type LowStockAlert } from './alerts'

const alert = (id: string): LowStockAlert => ({
  product: { id, name: id } as Product,
  sellableUnits: 10,
  minimumStock: 60,
})

describe('products that just crossed their minimum', () => {
  it('announces only those that were not below it before', () => {
    expect(newlyLow(new Set(['a']), [alert('a'), alert('b')]).map((a) => a.product.id)).toEqual(['b'])
  })

  it('announces nothing on the first load: those alerts were already there', () => {
    expect(newlyLow(null, [alert('a')])).toEqual([])
  })

  it('announces nothing when a product goes back above its minimum (a purchase arrived)', () => {
    expect(newlyLow(new Set(['a', 'b']), [alert('a')])).toEqual([])
  })
})

describe('expiration in words', () => {
  it('says how long ago or how soon, with yesterday, today and tomorrow in words', () => {
    expect(describeDaysLeft(-3)).toBe('Venció hace 3 días')
    expect(describeDaysLeft(-1)).toBe('Venció ayer')
    expect(describeDaysLeft(0)).toBe('Vence hoy')
    expect(describeDaysLeft(1)).toBe('Vence mañana')
    expect(describeDaysLeft(45)).toBe('Vence en 45 días')
  })
})
