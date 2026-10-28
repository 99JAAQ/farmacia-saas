import { afterEach, describe, expect, it, vi } from 'vitest'
import { formatDate, formatMoney, todayLocal } from './format'

describe('format', () => {
  afterEach(() => vi.useRealTimers())

  it('formats pesos the Colombian way', () => {
    // Intl may use a non-breaking space after "$": normalize it for the assertion.
    expect(formatMoney(87000).replace(/\s/g, ' ')).toBe('$ 87.000')
    expect(formatMoney(241.6667).replace(/\s/g, ' ')).toBe('$ 241,67')
  })

  it('shows a DateOnly as day/month/year without shifting it', () => {
    expect(formatDate('2026-10-01')).toBe('01/10/2026')
  })

  it('todayLocal uses the local date, not the UTC one', () => {
    // 23 Oct 2026, 20:00 local time: in UTC (Colombia + 5h) it is already the 24th.
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 9, 23, 20, 0, 0))

    expect(todayLocal()).toBe('2026-10-23')
  })
})
