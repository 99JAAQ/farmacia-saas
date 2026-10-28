import { describe, expect, it } from 'vitest'
import { describeDifference } from './labels'

const normalize = (text: string) => text.replace(/\s/g, ' ')

describe('describeDifference', () => {
  it('says the register balanced when there is no difference', () => {
    expect(describeDifference(0)).toEqual({ kind: 'exact', text: 'La caja cuadró exacto.' })
  })

  it('calls a positive difference a surplus', () => {
    const result = describeDifference(500)
    expect(result.kind).toBe('surplus')
    expect(normalize(result.text)).toBe('Sobrante de $ 500.')
  })

  it('calls a negative difference a shortage, shown without the minus sign', () => {
    const result = describeDifference(-2000)
    expect(result.kind).toBe('shortage')
    expect(normalize(result.text)).toBe('Faltante de $ 2.000.')
  })
})
