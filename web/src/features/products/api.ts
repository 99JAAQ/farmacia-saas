import { api } from '@/lib/api/http'

export interface SaleUnit {
  id: string
  name: string
  /** How many base units it contains (a box of 30 tablets = 30). */
  conversionFactor: number
  price: number
  barcode: string | null
}

export interface Product {
  id: string
  name: string
  baseUnitName: string
  requiresPrescription: boolean
  activeIngredient: string | null
  concentration: string | null
  pharmaceuticalForm: string | null
  sanitaryRegistration: string | null
  categoryId: string | null
  /** Largest presentation first (the API orders them). */
  saleUnits: SaleUnit[]
}

export const productKeys = {
  search: (term: string) => ['products', 'search', term] as const,
}

/** By name or active ingredient (accents do not matter) or by exact barcode. */
export function searchProducts(search: string, signal?: AbortSignal): Promise<Product[]> {
  return api<Product[]>(`/api/products?search=${encodeURIComponent(search)}`, { signal })
}
