import { formatMoney } from '@/lib/format'
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

/** The chosen customer's credit, for a sale with a "fiado" part. */
export interface CreditCustomer {
  available: number
  isSuspended: boolean
}

/**
 * The same rules as Sale.AddPayments on the server, to warn before charging (the server checks them again):
 * card, wallet, transfer and credit pay an exact amount and cannot go over the total (nobody gives change on a
 * transfer); cash covers the rest, and what is handed over beyond it is the change. Credit needs a customer whose
 * account is not suspended, and cannot go over what is available.
 */
export function summarizePayment(
  total: number,
  nonCash: NonCashPayment[],
  cashReceived: number | null,
  creditCustomer: CreditCustomer | null = null,
): PaymentSummary {
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
  if (nonCashTotal > total) return { ...summary, problem: 'Los pagos que no son en efectivo superan el total.' }

  const credit = others.find((payment) => payment.method === 'Credit')?.amount ?? 0
  if (credit > 0) {
    if (creditCustomer === null) return { ...summary, problem: 'Elija el cliente al que le fía.' }
    if (creditCustomer.isSuspended) return { ...summary, problem: 'El fiado de este cliente está suspendido.' }
    if (credit > creditCustomer.available)
      return { ...summary, problem: `El fiado supera el cupo disponible del cliente (${formatMoney(creditCustomer.available)}).` }
  }

  if (missing > 0) return { ...summary, problem: 'Falta dinero para cubrir el total.' }
  return summary
}
