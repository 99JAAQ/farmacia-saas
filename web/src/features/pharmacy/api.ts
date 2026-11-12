import { api } from '@/lib/api/http'

/** The signed-in user's pharmacy: its ticket header and its settings. */
export interface Pharmacy {
  id: string
  name: string
  /** NIT with its verification digit; it does not change (the platform registered the pharmacy with it). */
  taxId: string
  address: string | null
  phone: string | null
  isVatResponsible: boolean
}

export interface PharmacyDetails {
  name: string
  address: string | null
  phone: string | null
  isVatResponsible: boolean
}

export const pharmacyKeys = {
  current: ['pharmacy'] as const,
}

export function getPharmacy(signal?: AbortSignal): Promise<Pharmacy> {
  return api<Pharmacy>('/api/pharmacy', { signal })
}

/** The owner only (the API enforces it). */
export function updatePharmacy(details: PharmacyDetails): Promise<void> {
  return api<void>('/api/pharmacy', { method: 'PUT', body: details })
}

/** The lines of a printed header: name, NIT, then address and phone when the pharmacy has them. */
export function headerLines(pharmacy: Pharmacy | undefined): string[] {
  if (pharmacy === undefined) return ['Farmacia']
  return [pharmacy.name, `NIT ${pharmacy.taxId}`, pharmacy.address, pharmacy.phone && `Tel. ${pharmacy.phone}`].filter(
    (line): line is string => typeof line === 'string' && line !== '',
  )
}
