import type { StockMovementType } from './api'

export const movementTypeLabels: Record<StockMovementType, string> = {
  Purchase: 'Compra',
  Sale: 'Venta',
  Adjustment: 'Ajuste por conteo',
  ExpiredWriteOff: 'Baja por vencimiento',
  CustomerReturn: 'Devolución de cliente',
}

/** "+30", "−2", "0": the sign always shown, with a real minus sign. */
export function formatChange(change: number): string {
  if (change > 0) return `+${change}`
  if (change < 0) return `−${Math.abs(change)}`
  return '0'
}

/** What a count would record, before confirming it: the difference with the batch's stock, or why it cannot be saved. */
export function describeCount(currentQuantity: number, countedText: string): { difference: number | null; problem: string | null } {
  const counted = Number(countedText)
  if (countedText.trim() === '' || !Number.isInteger(counted) || counted < 0)
    return { difference: null, problem: 'Escriba cuántas unidades contó (un número entero, 0 o más).' }
  if (counted === currentQuantity) return { difference: 0, problem: 'El conteo coincide con el sistema: no hay nada que ajustar.' }
  return { difference: counted - currentQuantity, problem: null }
}
