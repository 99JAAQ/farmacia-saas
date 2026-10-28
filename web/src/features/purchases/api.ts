import { api } from '@/lib/api/http'

export type PurchaseStatus = 'Draft' | 'Confirmed'
export type PaymentTerm = 'Cash' | 'Credit'
export type VatRate = 'Exempt' | 'Reduced' | 'Standard'

export interface PurchaseSummary {
  id: string
  supplierName: string
  invoiceNumber: string
  invoiceDate: string
  status: PurchaseStatus
  itemCount: number
  total: number
}

export interface PurchaseTotals {
  gross: number
  lineDiscounts: number
  invoiceDiscount: number
  taxableBase: number
  vat: number
  total: number
}

export interface PurchaseItem {
  id: string
  productId: string
  productName: string
  saleUnitId: string
  saleUnitName: string
  unitsPerPackage: number
  quantity: number
  bonusQuantity: number
  unitCost: number
  lineDiscount: number
  vatRate: VatRate
  lotNumber: string
  expirationDate: string
  baseUnits: number
  netAmount: number
  batchId: string | null
  unitCostPerBaseUnit: number | null
}

export interface Purchase {
  id: string
  supplierId: string
  supplierName: string
  invoiceNumber: string
  invoiceDate: string
  paymentTerm: PaymentTerm
  dueDate: string | null
  invoiceDiscount: number
  status: PurchaseStatus
  confirmedAt: string | null
  totals: PurchaseTotals
  items: PurchaseItem[]
}

export interface PurchaseHeader {
  supplierId: string
  invoiceNumber: string
  invoiceDate: string
  paymentTerm: PaymentTerm
  dueDate: string | null
}

export const purchaseKeys = {
  all: ['purchases'] as const,
  list: (status?: PurchaseStatus) => ['purchases', 'list', status ?? 'all'] as const,
  detail: (id: string) => ['purchases', 'detail', id] as const,
}

export function listPurchases(status?: PurchaseStatus, signal?: AbortSignal): Promise<PurchaseSummary[]> {
  return api<PurchaseSummary[]>(status ? `/api/purchases?status=${status}` : '/api/purchases', { signal })
}

export function getPurchase(id: string, signal?: AbortSignal): Promise<Purchase> {
  return api<Purchase>(`/api/purchases/${id}`, { signal })
}

export async function createPurchaseDraft(header: PurchaseHeader): Promise<string> {
  const { id } = await api<{ id: string }>('/api/purchases', { method: 'POST', body: header })
  return id
}

export interface NewPurchaseItem {
  productId: string
  saleUnitId: string
  quantity: number
  bonusQuantity: number
  unitCost: number
  lineDiscount: number
  vatRate: VatRate
  lotNumber: string
  expirationDate: string
}

/** Saved right away: a power cut after this loses nothing. */
export async function addPurchaseItem(purchaseId: string, item: NewPurchaseItem): Promise<string> {
  const { id } = await api<{ id: string }>(`/api/purchases/${purchaseId}/items`, { method: 'POST', body: item })
  return id
}

export function removePurchaseItem(purchaseId: string, itemId: string): Promise<void> {
  return api<void>(`/api/purchases/${purchaseId}/items/${itemId}`, { method: 'DELETE' })
}

export function setInvoiceDiscount(purchaseId: string, discount: number): Promise<void> {
  return api<void>(`/api/purchases/${purchaseId}/invoice-discount`, { method: 'PUT', body: { discount } })
}

/** The batches enter the inventory. Fails (400) if the printed total differs by more than $1. */
export function confirmPurchase(purchaseId: string, printedInvoiceTotal: number): Promise<Purchase> {
  return api<Purchase>(`/api/purchases/${purchaseId}/confirm`, { method: 'POST', body: { printedInvoiceTotal } })
}
