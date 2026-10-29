import type { Cart, CartLine } from './cart'

/**
 * The cart survives a reload or a power cut: the cashier does not scan everything again.
 * One key per pharmacy and user, so another cashier on the same computer does not inherit it.
 * A cart left from another day is dropped: those prices and that customer are gone.
 */
const maxAgeMs = 12 * 60 * 60 * 1000

interface Saved {
  version: 1
  savedAt: number
  lines: CartLine[]
}

export function cartKey(pharmacyId: string, userId: string): string {
  return `farmacia:pos-cart:${pharmacyId}:${userId}`
}

export function loadCart(storage: Storage, key: string, now = Date.now()): Cart {
  try {
    const saved = JSON.parse(storage.getItem(key) ?? 'null') as Saved | null
    if (saved?.version !== 1 || !Array.isArray(saved.lines) || now - saved.savedAt > maxAgeMs) return []
    return saved.lines.filter(isLine)
  } catch {
    return [] // unreadable or blocked storage: start empty rather than break the register
  }
}

export function saveCart(storage: Storage, key: string, cart: Cart, now = Date.now()): void {
  try {
    if (cart.length === 0) storage.removeItem(key)
    else storage.setItem(key, JSON.stringify({ version: 1, savedAt: now, lines: cart } satisfies Saved))
  } catch {
    // Full or blocked storage: the sale goes on, only the reload protection is lost.
  }
}

function isLine(value: unknown): value is CartLine {
  const line = value as CartLine
  return (
    typeof line?.productId === 'string' &&
    typeof line.saleUnitId === 'string' &&
    Number.isInteger(line.quantity) &&
    line.quantity > 0 &&
    Array.isArray(line.units)
  )
}
