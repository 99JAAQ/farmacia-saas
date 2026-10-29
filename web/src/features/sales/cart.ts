import type { ProductSelection } from '@/features/products/scan'

export interface CartUnit {
  id: string
  name: string
  price: number
}

/** One line of the cart: a product in one presentation. Prices are copies, for the estimate only. */
export interface CartLine {
  productId: string
  productName: string
  requiresPrescription: boolean
  saleUnitId: string
  quantity: number
  /** The product's presentations, so the line can switch between box, blister and unit. */
  units: CartUnit[]
}

export type Cart = CartLine[]

export const emptyCart: Cart = []

const sameLine = (line: CartLine, productId: string, saleUnitId: string) =>
  line.productId === productId && line.saleUnitId === saleUnitId

export function unitOf(line: CartLine): CartUnit {
  return line.units.find((unit) => unit.id === line.saleUnitId) ?? { id: line.saleUnitId, name: '', price: 0 }
}

/**
 * Scanning the same box again adds one more instead of a second line (a reader may scan it three times).
 * A new line goes on top: the cashier looks at what was just scanned.
 */
export function addToCart(cart: Cart, { product, saleUnit }: ProductSelection): Cart {
  if (cart.some((line) => sameLine(line, product.id, saleUnit.id)))
    return cart.map((line) => (sameLine(line, product.id, saleUnit.id) ? { ...line, quantity: line.quantity + 1 } : line))

  const line: CartLine = {
    productId: product.id,
    productName: product.name,
    requiresPrescription: product.requiresPrescription,
    saleUnitId: saleUnit.id,
    quantity: 1,
    units: product.saleUnits.map(({ id, name, price }) => ({ id, name, price })),
  }
  return [line, ...cart]
}

/** Whole units of at least 1; anything else is ignored (removing a line is an explicit action). */
export function setQuantity(cart: Cart, productId: string, saleUnitId: string, quantity: number): Cart {
  if (!Number.isInteger(quantity) || quantity < 1) return cart
  return cart.map((line) => (sameLine(line, productId, saleUnitId) ? { ...line, quantity } : line))
}

/** Box -> blister, say. If the product already has a line in that presentation, both merge. */
export function changeUnit(cart: Cart, productId: string, fromUnitId: string, toUnitId: string): Cart {
  const moving = cart.find((line) => sameLine(line, productId, fromUnitId))
  if (!moving || fromUnitId === toUnitId || !moving.units.some((unit) => unit.id === toUnitId)) return cart

  const target = cart.find((line) => sameLine(line, productId, toUnitId))
  if (target)
    return cart
      .filter((line) => line !== moving)
      .map((line) => (line === target ? { ...line, quantity: line.quantity + moving.quantity } : line))

  return cart.map((line) => (line === moving ? { ...line, saleUnitId: toUnitId } : line))
}

export function removeLine(cart: Cart, productId: string, saleUnitId: string): Cart {
  return cart.filter((line) => !sameLine(line, productId, saleUnitId))
}

export function lineTotal(line: CartLine): number {
  return unitOf(line).price * line.quantity
}

/**
 * Estimated total, to show while the cart is built. Selling has no discounts, so this is plain arithmetic, but the
 * ticket's total (from the server, with the catalog's current prices) is the official one.
 */
export function estimatedTotal(cart: Cart): number {
  return cart.reduce((sum, line) => sum + lineTotal(line), 0)
}

/** The "Fórmula revisada" box is asked for only when some product needs it. */
export function needsPrescription(cart: Cart): boolean {
  return cart.some((line) => line.requiresPrescription)
}
