import { describe, expect, it } from 'vitest'
import { invalidationsFor } from './invalidations'

describe('what a real-time notice refreshes', () => {
  it('refreshes the stock screens and the shift when someone sells, once each', () => {
    const keys = invalidationsFor([
      { topic: 'sales', id: 's1' },
      { topic: 'stock', id: 'p1' },
      { topic: 'stock', id: 'p2' },
    ])

    expect(keys).toEqual([['sales'], ['cash-sessions'], ['products']])
  })

  it('refreshes a customer’s account when credit changes', () => {
    expect(invalidationsFor([{ topic: 'credit', id: 'c1' }])).toEqual([['customers']])
  })

  it('ignores a topic it does not know (a newer API): nothing breaks', () => {
    expect(invalidationsFor([{ topic: 'something-new', id: null }])).toEqual([])
  })
})
