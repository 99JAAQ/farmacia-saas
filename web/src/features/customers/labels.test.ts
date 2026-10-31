import { describe, expect, it } from 'vitest'
import type { CreditAccount } from './api'
import { describeCredit } from './labels'

const credit = (overrides: Partial<CreditAccount>): CreditAccount => ({
  limit: 50_000,
  balance: 0,
  available: 50_000,
  isSuspended: false,
  debtSince: null,
  daysOwing: null,
  ...overrides,
})

// Intl may use a non-breaking space after "$": normalize it for the assertions.
const text = (value: CreditAccount | null) => describeCredit(value).text.replace(/\s/g, ' ')

describe('describeCredit', () => {
  it('tells apart no credit, up to date and suspended', () => {
    expect(describeCredit(null)).toEqual({ text: 'Sin fiado', tone: 'muted' })
    expect(describeCredit(credit({}))).toEqual({ text: 'Al día', tone: 'success' })
    expect(describeCredit(credit({ isSuspended: true })).tone).toBe('destructive')
    expect(text(credit({ isSuspended: true, balance: 8_000 }))).toBe('Suspendido · debe $ 8.000')
  })

  it('says how much and for how long, and warns once the debt is over a month old', () => {
    expect(text(credit({ balance: 12_000, daysOwing: 0 }))).toBe('Debe $ 12.000 desde hoy')
    expect(text(credit({ balance: 12_000, daysOwing: 1 }))).toBe('Debe $ 12.000 hace 1 día')
    expect(describeCredit(credit({ balance: 12_000, daysOwing: 30 })).tone).toBe('muted')
    expect(describeCredit(credit({ balance: 12_000, daysOwing: 31 }))).toMatchObject({ tone: 'warning' })
  })
})
