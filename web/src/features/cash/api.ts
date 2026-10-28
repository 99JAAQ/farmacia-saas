import { api } from '@/lib/api/http'

export type CashSessionStatus = 'Open' | 'Closed'
export type CashMovementType = 'Income' | 'Expense'

export interface CashMovement {
  id: string
  type: CashMovementType
  amount: number
  reason: string
  registeredByName: string
  occurredAt: string
}

export interface CashSession {
  id: string
  status: CashSessionStatus
  openedBy: string
  openedByName: string
  openedAt: string
  openingFloat: number
  /** Only whoever opened the shift can record movements (and sell) in it. */
  isMine: boolean
  /** Null for the cashier while the shift is open: blind count (the server hides it, not just this screen). */
  netCashFromSales: number | null
  expectedCash: number | null
  countedCash: number | null
  difference: number | null
  closedByName: string | null
  closedAt: string | null
  closingNotes: string | null
  movements: CashMovement[]
}

export interface CurrentCashSession {
  session: CashSession | null
  /** What was counted at the last closing: what should be in the drawer now. */
  suggestedOpeningFloat: number
}

export const cashKeys = {
  current: ['cash-sessions', 'current'] as const,
}

export function getCurrentCashSession(signal?: AbortSignal): Promise<CurrentCashSession> {
  return api<CurrentCashSession>('/api/cash-sessions/current', { signal })
}

export async function openCashSession(openingFloat: number): Promise<string> {
  const { id } = await api<{ id: string }>('/api/cash-sessions', { method: 'POST', body: { openingFloat } })
  return id
}

export async function recordCashMovement(type: CashMovementType, amount: number, reason: string): Promise<string> {
  const { id } = await api<{ id: string }>('/api/cash-sessions/current/movements', {
    method: 'POST',
    body: { type, amount, reason },
  })
  return id
}

/** The answer reveals the expected cash and the difference. */
export function closeCashSession(id: string, countedCash: number, notes: string | null): Promise<CashSession> {
  return api<CashSession>(`/api/cash-sessions/${id}/close`, { method: 'POST', body: { countedCash, notes } })
}
