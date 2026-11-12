import { describe, expect, it } from 'vitest'
import { chartHours } from './api'

describe('hours in the sales chart', () => {
  it('shows the opening hours, with the hours without sales at zero', () => {
    const hours = chartHours([{ hour: 9, total: 24_000, sales: 2 }])

    expect(hours[0]).toEqual({ hour: 7, total: 0, sales: 0 })
    expect(hours.at(-1)?.hour).toBe(19)
    expect(hours.find((h) => h.hour === 9)?.total).toBe(24_000)
    expect(hours).toHaveLength(13)
  })

  it('widens the range for a sale outside the usual hours (an emergency at night)', () => {
    const hours = chartHours([{ hour: 22, total: 5_000, sales: 1 }])

    expect(hours.at(-1)).toEqual({ hour: 22, total: 5_000, sales: 1 })
    expect(hours[0].hour).toBe(7)
  })
})
