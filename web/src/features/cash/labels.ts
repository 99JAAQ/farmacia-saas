import { formatMoney } from '@/lib/format'
import type { CashMovementType } from './api'

export const movementTypeLabels: Record<CashMovementType, string> = {
  Income: 'Ingreso',
  Expense: 'Egreso',
}

export type DifferenceKind = 'exact' | 'surplus' | 'shortage'

/** Counted minus expected, in words: what the cashier reads after closing. */
export function describeDifference(difference: number): { kind: DifferenceKind; text: string } {
  if (difference === 0) return { kind: 'exact', text: 'La caja cuadró exacto.' }
  if (difference > 0) return { kind: 'surplus', text: `Sobrante de ${formatMoney(difference)}.` }
  return { kind: 'shortage', text: `Faltante de ${formatMoney(-difference)}.` }
}
