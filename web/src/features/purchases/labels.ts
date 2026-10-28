import type { PaymentTerm, PurchaseStatus, VatRate } from './api'

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
