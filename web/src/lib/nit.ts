/**
 * Colombian NIT verification digit (DIAN modulo 11). Same algorithm as the backend's Nit value object
 * (src/Farmacia.Domain/Common/Nit.cs) and tested with the same real NITs: it only gives instant feedback while
 * typing; the API is still the authority and validates on save.
 */

// DIAN weights, applied to the digits from right to left.
const WEIGHTS = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71]

/** Digits only, without dots or spaces (what people type). Null if it cannot be a NIT number. */
export function normalizeNitNumber(input: string): string | null {
  const digits = input.replace(/[.\s]/g, '')
  return /^\d{1,15}$/.test(digits) ? digits : null
}

export function nitVerificationDigit(number: string): number {
  let sum = 0
  for (let i = 0; i < number.length; i++) {
    sum += Number(number[number.length - 1 - i]) * WEIGHTS[i]
  }
  const remainder = sum % 11
  return remainder > 1 ? 11 - remainder : remainder
}

/** "800.197.268" -> "800197268-4", the format the API expects. Null if the input is not a NIT number. */
export function toFullNit(input: string): string | null {
  const number = normalizeNitNumber(input)
  return number === null ? null : `${number}-${nitVerificationDigit(number)}`
}
