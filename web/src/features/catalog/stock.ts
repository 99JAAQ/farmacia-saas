import type { SaleUnit } from '@/features/products/api'

/** Spanish plural of a unit name, enough for pharmacy units: tableta → tabletas, unidad → unidades. */
export function pluralize(word: string, count: number): string {
  const lower = word.toLowerCase()
  if (count === 1) return lower
  return /[aeiouáéíóú]$/.test(lower) ? `${lower}s` : `${lower}es`
}

/**
 * Stock in counter words: 95 tablets with a box of 30 reads "3 × Caja x30 + 5 tabletas". Stock is kept in base
 * units; this only expresses it in the largest presentation and what is left loose.
 */
export function formatStock(units: number, saleUnits: SaleUnit[], baseUnitName: string): string {
  if (units <= 0) return 'Sin stock'

  const largest = saleUnits.reduce<SaleUnit | null>(
    (best, unit) => (unit.conversionFactor > 1 && (best === null || unit.conversionFactor > best.conversionFactor) ? unit : best),
    null,
  )
  if (largest === null) return `${units} ${pluralize(baseUnitName, units)}`

  const whole = Math.floor(units / largest.conversionFactor)
  const loose = units % largest.conversionFactor
  const parts = [
    whole > 0 ? `${whole} × ${largest.name}` : null,
    loose > 0 ? `${loose} ${pluralize(baseUnitName, loose)}` : null,
  ]
  return parts.filter((part) => part !== null).join(' + ')
}

/** Expiring within this many days is a warning: time to sell it first or to return it to the supplier. */
export const expiringSoonDays = 90

/** Days from today (the pharmacy's "YYYY-MM-DD") to a DateOnly, without Date objects and time zones. */
export function daysUntil(isoDate: string, today: string): number {
  const toDays = (value: string) => {
    const [year, month, day] = value.split('-').map(Number)
    return Date.UTC(year, month - 1, day) / 86_400_000
  }
  return toDays(isoDate) - toDays(today)
}
