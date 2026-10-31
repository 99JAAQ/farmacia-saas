import { api } from '@/lib/api/http'

export type DocumentType =
  | 'IdentityCard'
  | 'CitizenshipCard'
  | 'ForeignerId'
  | 'Nit'
  | 'Passport'
  | 'SpecialStayPermit'
  | 'TemporaryProtectionPermit'

export interface CreditAccount {
  /** The most the customer may owe. */
  limit: number
  /** What the customer owes now. */
  balance: number
  available: number
  isSuspended: boolean
  debtSince: string | null
  /** Days owing without interruption; null when nothing is owed. */
  daysOwing: number | null
}

export interface Customer {
  id: string
  fullName: string
  documentType: DocumentType
  documentNumber: string
  phone: string | null
  email: string | null
  /** Null when the owner has not enabled credit ("fiado") for the customer. */
  credit: CreditAccount | null
}

export interface NewCustomer {
  fullName: string
  documentType: DocumentType
  /** A NIT goes with its verification digit, e.g. "800987654-4". */
  documentNumber: string
  phone?: string
  email?: string
}

export type CreditEntryType = 'Sale' | 'Payment' | 'Return'

export interface CreditEntry {
  type: CreditEntryType
  /** Positive when the debt went up, negative when it went down. */
  change: number
  balanceAfter: number
  occurredAt: string
  referenceId: string
}

export interface CreditStatement {
  customerId: string
  customerName: string
  account: CreditAccount
  /** Newest first. */
  entries: CreditEntry[]
}

export interface Debtor {
  customerId: string
  customerName: string
  documentNumber: string
  phone: string | null
  account: CreditAccount
}

export const customerKeys = {
  all: ['customers'] as const,
  search: (term: string) => ['customers', 'search', term] as const,
  detail: (id: string) => ['customers', id] as const,
  statement: (id: string) => ['customers', id, 'credit'] as const,
  debtors: ['customers', 'debtors'] as const,
}

/** By name (accents do not matter) or by exact document number; all of them if empty. */
export function searchCustomers(search: string, signal?: AbortSignal): Promise<Customer[]> {
  return api<Customer[]>(`/api/customers?search=${encodeURIComponent(search)}`, { signal })
}

export function getCustomer(id: string, signal?: AbortSignal): Promise<Customer> {
  return api<Customer>(`/api/customers/${id}`, { signal })
}

export async function createCustomer(customer: NewCustomer): Promise<string> {
  const { id } = await api<{ id: string }>('/api/customers', { method: 'POST', body: customer })
  return id
}

export function getCreditStatement(customerId: string, signal?: AbortSignal): Promise<CreditStatement> {
  return api<CreditStatement>(`/api/customers/${customerId}/credit`, { signal })
}

export function listDebtors(signal?: AbortSignal): Promise<Debtor[]> {
  return api<Debtor[]>('/api/customers/debtors', { signal })
}

export function openCredit(customerId: string, limit: number): Promise<void> {
  return api<void>(`/api/customers/${customerId}/credit`, { method: 'POST', body: { limit } })
}

export function changeCreditLimit(customerId: string, limit: number): Promise<void> {
  return api<void>(`/api/customers/${customerId}/credit/limit`, { method: 'PUT', body: { limit } })
}

export function setCreditSuspended(customerId: string, suspended: boolean): Promise<void> {
  return api<void>(`/api/customers/${customerId}/credit/${suspended ? 'suspend' : 'resume'}`, { method: 'POST' })
}

export type CreditPaymentMethod = 'Cash' | 'Card' | 'DigitalWallet' | 'BankTransfer'

/** The payment voucher ("abono"). */
export interface CreditPayment {
  id: string
  number: number
  customerId: string
  customerName: string
  receivedAt: string
  cashSessionId: string
  receivedByName: string
  method: CreditPaymentMethod
  amount: number
  /** What the customer still owes right after this payment. */
  balanceAfter: number
}

/** In the current user's open shift; answers with the whole voucher, ready to print. */
export function registerCreditPayment(customerId: string, method: CreditPaymentMethod, amount: number): Promise<CreditPayment> {
  return api<CreditPayment>('/api/credit-payments', { method: 'POST', body: { customerId, method, amount } })
}
