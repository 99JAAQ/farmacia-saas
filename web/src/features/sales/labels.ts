import type { PaymentMethod } from './api'
import type { NonCashMethod } from './payment'

export const paymentMethodLabels: Record<PaymentMethod, string> = {
  Cash: 'Efectivo',
  Card: 'Tarjeta',
  DigitalWallet: 'Nequi / Daviplata',
  BankTransfer: 'Transferencia',
}

export const nonCashMethods: NonCashMethod[] = ['Card', 'DigitalWallet', 'BankTransfer']
