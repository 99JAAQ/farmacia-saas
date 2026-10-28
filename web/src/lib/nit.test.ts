import { describe, expect, it } from 'vitest'
import { normalizeNitNumber, toFullNit } from './nit'

// Same cases as tests/Farmacia.Domain.Tests/Common/NitTests.cs: if the two implementations ever differ,
// one of the two test suites fails.
describe('toFullNit', () => {
  it.each([
    ['800197268', '800197268-4'], // DIAN
    ['890903938', '890903938-8'], // Bancolombia
    ['900123455', '900123455-0'], // remainder 0 -> digit 0
    ['900123459', '900123459-1'], // remainder 1 -> digit 1
    ['800.197.268', '800197268-4'],
    [' 800 197 268 ', '800197268-4'],
  ])('%s -> %s', (input, expected) => {
    expect(toFullNit(input)).toBe(expected)
  })

  it.each(['', '80019726A', '800197268-4', '1234567890123456'])('rejects %j', (input) => {
    expect(toFullNit(input)).toBeNull()
  })
})

describe('normalizeNitNumber', () => {
  it('keeps only the digits people type with dots or spaces', () => {
    expect(normalizeNitNumber('900.123.456')).toBe('900123456')
  })
})
