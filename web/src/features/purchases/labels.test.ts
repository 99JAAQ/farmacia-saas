import { describe, expect, it } from 'vitest'
import { describeDue, paymentProblem } from './labels'

describe('purchase payables labels', () => {
  it('says when an invoice is due, and warns as the date gets close', () => {
    expect(describeDue(-38)).toEqual({ text: 'Vencida hace 38 días', tone: 'destructive' })
    expect(describeDue(-1).text).toBe('Vencida hace 1 día')
    expect(describeDue(0)).toEqual({ text: 'Vence hoy', tone: 'warning' })
    expect(describeDue(1).text).toBe('Vence mañana')
    expect(describeDue(7).tone).toBe('warning')
    expect(describeDue(8)).toEqual({ text: 'Vence en 8 días', tone: 'muted' })
    expect(describeDue(null).tone).toBe('muted')
  })

  it('applies the server rules for a payment before saving it', () => {
    expect(paymentProblem('40000', 90_000, 'BankTransfer', 'TRX-1')).toBeNull()
    expect(paymentProblem('90000', 90_000, 'Cash', '')).toBeNull() // exactly the balance, from the safe: no number
    expect(paymentProblem('90001', 90_000, 'Cash', '')).toContain('saldo')
    expect(paymentProblem('1000', 90_000, 'Check', '  ')).toContain('número')
    for (const invalid of ['', '0', '-5', 'abc']) expect(paymentProblem(invalid, 90_000, 'Cash', '')).not.toBeNull()
  })
})
