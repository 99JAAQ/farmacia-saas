import type { PaymentMethod } from './api'

export type NonCashMethod = Exclude<PaymentMethod, 'Cash'>

export interface NonCashPayment {
  method: NonCashMethod
  amount: number
}

/** A card, wallet or transfer payment as typed (text, so the field can be cleared while typing). */
export interface NonCashRow {
  method: NonCashMethod
  text: string
}

/** A typed amount: empty, negative or not a number counts as nothing. */
export function toAmount(text: string): number {
  const amount = Number(text)
  return text.trim() !== '' && Number.isFinite(amount) && amount > 0 ? amount : 0
}

export interface PaymentSummary {
  nonCashTotal: number
  /** What cash must cover after the other methods. */
  cashDue: number
  /** Still missing to cover the total; 0 when covered. */
  missing: number
  change: number
  /** Why "Cobrar" cannot go ahead yet; null when it can. */
  problem: string | null
  /** What to send: non-cash at their amount, and cash (what was handed over) only if there is cash due. */
  payments: { method: PaymentMethod; amount: number }[]
}

/**
 * The same rules as Sale.AddPayments on the server, to warn before charging (the server checks them again):
 * card, wallet and transfer pay an exact amount and cannot go over the total (nobody gives change on a transfer);
 * cash covers the rest, and what is handed over beyond it is the change.
 */
export function summarizePayment(total: number, nonCash: NonCashPayment[], cashReceived: number | null): PaymentSummary {
  const others = nonCash.filter((payment) => payment.amount > 0)
  const nonCashTotal = others.reduce((sum, payment) => sum + payment.amount, 0)
  const cashDue = Math.max(total - nonCashTotal, 0)
  const cash = cashDue > 0 ? (cashReceived ?? 0) : 0
  const missing = Math.max(cashDue - cash, 0)

  const summary: PaymentSummary = {
    nonCashTotal,
    cashDue,
    missing,
    change: cashDue > 0 ? Math.max(cash - cashDue, 0) : 0,
    problem: null,
    payments: [...(cash > 0 ? [{ method: 'Cash' as const, amount: cash }] : []), ...others],
  }

  if (total <= 0) return { ...summary, problem: 'Agregue productos a la venta.' }
  if (nonCashTotal > total)
    return { ...summary, problem: 'Los pagos con tarjeta, billetera o transferencia superan el total.' }
  if (missing > 0) return { ...summary, problem: 'Falta dinero para cubrir el total.' }
  return summary
}
