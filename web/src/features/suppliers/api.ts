import { api } from '@/lib/api/http'

export interface Supplier {
  id: string
  name: string
  taxId: string
  contactName: string | null
  phone: string | null
  email: string | null
}

export interface NewSupplier {
  name: string
  /** Full NIT with its verification digit, e.g. "800987654-4". */
  taxId: string
  contactName?: string
  phone?: string
  email?: string
}

export function searchSuppliers(search: string, signal?: AbortSignal): Promise<Supplier[]> {
  return api<Supplier[]>(`/api/suppliers?search=${encodeURIComponent(search)}`, { signal })
}

export async function createSupplier(supplier: NewSupplier): Promise<string> {
  const { id } = await api<{ id: string }>('/api/suppliers', { method: 'POST', body: supplier })
  return id
}
