import type { PaymentTerm, PurchaseStatus, SupplierPaymentMethod, VatRate } from './api'

export const statusLabels: Record<PurchaseStatus, string> = {
  Draft: 'Borrador',
  Confirmed: 'Confirmada',
}

export const paymentTermLabels: Record<PaymentTerm, string> = {
  Cash: 'Contado',
  Credit: 'Crédito',
}

export const vatRateLabels: Record<VatRate, string> = {
  Exempt: '0%',
  Reduced: '5%',
  Standard: '19%',
}

export const supplierPaymentMethodLabels: Record<SupplierPaymentMethod, string> = {
  BankTransfer: 'Transferencia',
  Cash: 'Efectivo (caja fuerte)',
  Check: 'Cheque',
}

/** When an owed invoice is due, in words, and how urgent it is. */
export function describeDue(daysToDue: number | null): { text: string; tone: 'muted' | 'warning' | 'destructive' } {
  if (daysToDue === null) return { text: 'Sin fecha de pago', tone: 'muted' }
  if (daysToDue < 0) return { text: daysToDue === -1 ? 'Vencida hace 1 día' : `Vencida hace ${-daysToDue} días`, tone: 'destructive' }
  if (daysToDue === 0) return { text: 'Vence hoy', tone: 'warning' }
  return { text: daysToDue === 1 ? 'Vence mañana' : `Vence en ${daysToDue} días`, tone: daysToDue <= 7 ? 'warning' : 'muted' }
}

/** The same rules as Purchase.RegisterPayment, to warn before saving (the server checks them again). */
export function paymentProblem(amountText: string, balance: number, method: SupplierPaymentMethod, reference: string): string | null {
  const amount = Number(amountText)
  if (amountText.trim() === '' || !Number.isFinite(amount) || amount <= 0) return 'Escriba el valor del pago.'
  if (amount > balance) return 'El pago supera el saldo de la factura.'
  if (method !== 'Cash' && reference.trim() === '') return 'Escriba el número de la transferencia o del cheque.'
  return null
}
