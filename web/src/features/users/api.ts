import { api } from '@/lib/api/http'
import type { Role } from '@/lib/api/types'

/** A pharmacy role: the owner never creates platform administrators. */
export type PharmacyRole = Exclude<Role, 'PlatformAdmin'>

export const pharmacyRoles: PharmacyRole[] = ['Cashier', 'StockKeeper', 'Owner']

export interface UserAccount {
  id: string
  fullName: string
  email: string
  role: PharmacyRole
  isActive: boolean
  /** Still has the temporary password the owner gave them. */
  mustChangePassword: boolean
}

export interface NewUser {
  fullName: string
  email: string
  role: PharmacyRole
  temporaryPassword: string
}

export const userKeys = {
  all: ['users'] as const,
}

export function listUsers(signal?: AbortSignal): Promise<UserAccount[]> {
  return api<UserAccount[]>('/api/users', { signal })
}

export async function createUser(user: NewUser): Promise<string> {
  const { id } = await api<{ id: string }>('/api/users', { method: 'POST', body: user })
  return id
}

export function changeUserRole(id: string, role: PharmacyRole): Promise<void> {
  return api<void>(`/api/users/${id}/role`, { method: 'PUT', body: { role } })
}

/** Deactivating also signs them out (within 15 minutes at most). */
export function setUserActive(id: string, isActive: boolean): Promise<void> {
  return api<void>(`/api/users/${id}/${isActive ? 'activate' : 'deactivate'}`, { method: 'POST' })
}

export function resetUserPassword(id: string, temporaryPassword: string): Promise<void> {
  return api<void>(`/api/users/${id}/reset-password`, { method: 'POST', body: { temporaryPassword } })
}

/** Active users first, and among them, the ones still to change their temporary password. */
export function describeStatus(user: UserAccount): { text: string; tone: 'success' | 'warning' | 'muted' } {
  if (!user.isActive) return { text: 'Desactivado', tone: 'muted' }
  if (user.mustChangePassword) return { text: 'Falta cambiar la contraseña temporal', tone: 'warning' }
  return { text: 'Activo', tone: 'success' }
}
