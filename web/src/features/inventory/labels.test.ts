import { describe, expect, it } from 'vitest'
import { describeCount, formatChange } from './labels'

describe('inventory labels', () => {
  it('always shows the sign of a change, with a real minus sign', () => {
    expect([formatChange(30), formatChange(-2), formatChange(0)]).toEqual(['+30', '−2', '0'])
  })

  it('previews what a count records, or why it cannot be saved', () => {
    expect(describeCount(40, '38')).toEqual({ difference: -2, problem: null })
    expect(describeCount(40, '45')).toEqual({ difference: 5, problem: null })
    expect(describeCount(40, '0')).toEqual({ difference: -40, problem: null }) // the whole batch is gone
    expect(describeCount(40, '40').problem).toContain('coincide')
    for (const invalid of ['', '-1', '2.5', 'abc']) expect(describeCount(40, invalid).problem).not.toBeNull()
  })
})
