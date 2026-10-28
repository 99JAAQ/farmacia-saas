import type { Product, SaleUnit } from './api'

export interface ProductSelection {
  product: Product
  saleUnit: SaleUnit
}

/**
 * What a typed or scanned text points to, when pressing Enter:
 * - an exact barcode: that product with that exact presentation (a scanned box is a box, not a tablet);
 * - otherwise, a single product found: that product with its largest presentation (how suppliers invoice);
 * - otherwise (none or several): nothing, the user picks from the list.
 */
export function findScannedUnit(products: Product[], text: string): ProductSelection | null {
  const code = text.trim()
  if (!code) return null

  for (const product of products) {
    const saleUnit = product.saleUnits.find((unit) => unit.barcode === code)
    if (saleUnit) return { product, saleUnit }
  }

  if (products.length === 1 && products[0].saleUnits.length > 0)
    return { product: products[0], saleUnit: products[0].saleUnits[0] }

  return null
}
