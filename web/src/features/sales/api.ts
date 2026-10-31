import type { VatRate } from '@/features/purchases/api'
import { api } from '@/lib/api/http'

export type PaymentMethod = 'Cash' | 'Card' | 'DigitalWallet' | 'BankTransfer' | 'Credit'

export interface SaleItem {
  id: string
  productId: string
  productName: string
  saleUnitName: string
  quantity: number
  unitPrice: number
  vatRate: VatRate
  total: number
  /** Included in the total (prices include VAT). */
  vat: number
  requiresPrescription: boolean
  /** Already returned, in the same sale unit: quantity minus this is what can still be returned. */
  returnedQuantity: number
}

export interface Payment {
  method: PaymentMethod
  /** The part of the sale paid with this method. */
  amount: number
  /** Cash only: what the customer handed over. */
  received: number | null
  change: number
}

/** The ticket. No costs: the cashier sees it. */
export interface Sale {
  id: string
  number: number
  soldAt: string
  cashSessionId: string
  soldByName: string
  total: number
  vat: number
  change: number
  /** When the sale has a customer (always when part of it is on credit). */
  customerName: string | null
  /** The part taken on credit ("fiado"). */
  creditAmount: number
  prescriptionChecked: boolean
  items: SaleItem[]
  payments: Payment[]
}

export interface SaleSummary {
  id: string
  number: number
  soldAt: string
  soldByName: string
  total: number
  /** Lines of the ticket, not units. */
  itemCount: number
  methods: PaymentMethod[]
}

/** What "Cobrar" sends. Never prices: the server takes them from the catalog. */
export interface RegisterSaleRequest {
  lines: { productId: string; saleUnitId: string; quantity: number }[]
  /** For cash, the amount the customer handed over. */
  payments: { method: PaymentMethod; amount: number }[]
  prescriptionChecked: boolean
  /** Required to take part of the sale on credit. */
  customerId?: string
}

export const saleKeys = {
  byShift: (cashSessionId: string) => ['sales', 'shift', cashSessionId] as const,
  detail: (id: string) => ['sales', id] as const,
  byNumber: (number: number) => ['sales', 'number', number] as const,
}

/** Answers with the whole ticket, ready to print. */
export function registerSale(request: RegisterSaleRequest): Promise<Sale> {
  return api<Sale>('/api/sales', { method: 'POST', body: request })
}

export function getSale(id: string, signal?: AbortSignal): Promise<Sale> {
  return api<Sale>(`/api/sales/${id}`, { signal })
}

/** By the number printed on the ticket: how a return starts at the counter. */
export function getSaleByNumber(number: number, signal?: AbortSignal): Promise<Sale> {
  return api<Sale>(`/api/sales/by-number/${number}`, { signal })
}

/** Newest first. */
export function listShiftSales(cashSessionId: string, signal?: AbortSignal): Promise<SaleSummary[]> {
  return api<SaleSummary[]>(`/api/sales?cashSessionId=${encodeURIComponent(cashSessionId)}`, { signal })
}
