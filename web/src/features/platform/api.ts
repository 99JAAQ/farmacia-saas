import { api } from '@/lib/api/http'

/** A pharmacy as the platform administrator sees it: who it is and who runs it, never its sales or stock. */
export interface PlatformPharmacy {
  id: string
  name: string
  taxId: string
  address: string | null
  phone: string | null
  isVatResponsible: boolean
  owners: { fullName: string; email: string }[]
  userCount: number
}

export interface NewPharmacy {
  name: string
  taxId: string
  address: string | null
  phone: string | null
  isVatResponsible: boolean
  ownerFullName: string
  ownerEmail: string
  /** The owner must change it at the first login. */
  ownerPassword: string
}

export const platformKeys = {
  pharmacies: ['platform', 'pharmacies'] as const,
}

export function listPharmacies(signal?: AbortSignal): Promise<PlatformPharmacy[]> {
  return api<PlatformPharmacy[]>('/api/platform/pharmacies', { signal })
}

/** The pharmacy and its owner, in one transaction: both are created, or neither is. */
export async function createPharmacy(pharmacy: NewPharmacy): Promise<string> {
  const { id } = await api<{ id: string }>('/api/platform/pharmacies', { method: 'POST', body: pharmacy })
  return id
}
