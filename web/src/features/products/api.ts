import type { VatRate } from '@/features/purchases/api'
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
  /** VAT included in the sale prices; most medicines are excluded (0%). */
  vatRate: VatRate
  activeIngredient: string | null
  concentration: string | null
  pharmaceuticalForm: string | null
  sanitaryRegistration: string | null
  categoryId: string | null
  /** Discontinued products are inactive: not sold nor bought, never deleted. */
  isActive: boolean
  /** Largest presentation first (the API orders them). */
  saleUnits: SaleUnit[]
}

/** A product with its stock, in base units. */
export interface CatalogItem {
  product: Product
  /** In batches that have not expired. */
  sellableUnits: number
  /** In expired batches: they cannot be sold, only written off. */
  expiredUnits: number
  /** The nearest expiration among the sellable batches (DateOnly). */
  nextExpiration: string | null
}

export interface ProductBatch {
  id: string
  lotNumber: string
  /** DateOnly, "YYYY-MM-DD". */
  expirationDate: string
  /** In base units. */
  quantity: number
  /** Per base unit. */
  unitCost: number
  receivedAt: string
  isExpired: boolean
}

export interface Category {
  id: string
  name: string
}

/** What the product form edits (the presentations are edited one by one). */
export interface ProductDetails {
  name: string
  baseUnitName: string
  requiresPrescription: boolean
  activeIngredient: string | null
  concentration: string | null
  pharmaceuticalForm: string | null
  sanitaryRegistration: string | null
  categoryId: string | null
  vatRate: VatRate
}

export interface SaleUnitInput {
  name: string
  conversionFactor: number
  price: number
  barcode: string | null
}

export const productKeys = {
  all: ['products'] as const,
  search: (term: string) => ['products', 'search', term] as const,
  catalog: (term: string, includeInactive: boolean) => ['products', 'catalog', term, includeInactive] as const,
  detail: (id: string) => ['products', id] as const,
  batches: (id: string) => ['products', id, 'batches'] as const,
  categories: ['categories'] as const,
}

/** By name or active ingredient (accents do not matter) or by exact barcode. Active products only: this is the POS. */
export function searchProducts(search: string, signal?: AbortSignal): Promise<Product[]> {
  return api<Product[]>(`/api/products?search=${encodeURIComponent(search)}`, { signal })
}

export function getCatalog(search: string, includeInactive: boolean, signal?: AbortSignal): Promise<CatalogItem[]> {
  return api<CatalogItem[]>(
    `/api/products/catalog?search=${encodeURIComponent(search)}&includeInactive=${includeInactive}`,
    { signal },
  )
}

export function getProduct(id: string, signal?: AbortSignal): Promise<Product> {
  return api<Product>(`/api/products/${id}`, { signal })
}

export function getProductBatches(id: string, signal?: AbortSignal): Promise<ProductBatch[]> {
  return api<ProductBatch[]>(`/api/products/${id}/batches`, { signal })
}

export async function createProduct(details: ProductDetails, firstUnit: SaleUnitInput): Promise<string> {
  const { id } = await api<{ id: string }>('/api/products', { method: 'POST', body: { ...details, saleUnits: [firstUnit] } })
  return id
}

export function updateProduct(id: string, details: ProductDetails): Promise<void> {
  return api<void>(`/api/products/${id}`, { method: 'PUT', body: details })
}

export function addSaleUnit(productId: string, unit: SaleUnitInput): Promise<{ id: string }> {
  return api<{ id: string }>(`/api/products/${productId}/sale-units`, { method: 'POST', body: unit })
}

export function updateSaleUnit(productId: string, saleUnitId: string, unit: SaleUnitInput): Promise<void> {
  return api<void>(`/api/products/${productId}/sale-units/${saleUnitId}`, { method: 'PUT', body: unit })
}

export function setProductActive(id: string, active: boolean): Promise<void> {
  return api<void>(`/api/products/${id}/${active ? 'activate' : 'deactivate'}`, { method: 'POST' })
}

export function listCategories(signal?: AbortSignal): Promise<Category[]> {
  return api<Category[]>('/api/categories', { signal })
}

export async function createCategory(name: string): Promise<string> {
  const { id } = await api<{ id: string }>('/api/categories', { method: 'POST', body: { name } })
  return id
}
