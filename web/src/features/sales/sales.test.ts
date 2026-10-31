import { describe, expect, it } from 'vitest'
import type { Product } from '@/features/products/api'
import { addToCart, changeUnit, emptyCart, estimatedTotal, needsPrescription, removeLine, setQuantity } from './cart'
import { cartKey, loadCart, saveCart } from './cartStorage'
import { summarizePayment, toAmount } from './payment'

const acetaminofen: Product = {
  id: 'acetaminofen',
  name: 'Acetaminofén 500 mg',
  baseUnitName: 'Tableta',
  requiresPrescription: false,
  activeIngredient: null,
  concentration: null,
  pharmaceuticalForm: null,
  sanitaryRegistration: null,
  categoryId: null,
  vatRate: 'Exempt',
  isActive: true,
  saleUnits: [
    { id: 'box', name: 'Caja x30', conversionFactor: 30, price: 12_000, barcode: '7702057000017' },
    { id: 'blister', name: 'Blíster x10', conversionFactor: 10, price: 4_500, barcode: null },
  ],
}
const box = acetaminofen.saleUnits[0]
const blister = acetaminofen.saleUnits[1]
const amoxicilina: Product = {
  ...acetaminofen,
  id: 'amoxicilina',
  name: 'Amoxicilina 500 mg',
  requiresPrescription: true,
  saleUnits: [{ id: 'amox-box', name: 'Caja x50', conversionFactor: 50, price: 30_000, barcode: null }],
}

describe('cart', () => {
  it('scanning the same box again adds one more instead of a second line', () => {
    let cart = addToCart(emptyCart, { product: acetaminofen, saleUnit: box })
    cart = addToCart(cart, { product: acetaminofen, saleUnit: box })

    expect(cart).toHaveLength(1)
    expect(cart[0].quantity).toBe(2)
    expect(estimatedTotal(cart)).toBe(24_000)
  })

  it('another presentation of the same product is its own line, and the newest goes on top', () => {
    let cart = addToCart(emptyCart, { product: acetaminofen, saleUnit: box })
    cart = addToCart(cart, { product: acetaminofen, saleUnit: blister })

    expect(cart.map((line) => line.saleUnitId)).toEqual(['blister', 'box'])
    expect(estimatedTotal(cart)).toBe(16_500)
  })

  it('a quantity must be a whole number of at least 1: anything else leaves the line as it was', () => {
    const cart = addToCart(emptyCart, { product: acetaminofen, saleUnit: box })

    expect(setQuantity(cart, 'acetaminofen', 'box', 3)[0].quantity).toBe(3)
    for (const invalid of [0, -1, 1.5, Number.NaN]) expect(setQuantity(cart, 'acetaminofen', 'box', invalid)).toBe(cart)
  })

  it('switching a line to a presentation already in the cart merges both lines', () => {
    let cart = addToCart(emptyCart, { product: acetaminofen, saleUnit: box })
    cart = setQuantity(addToCart(cart, { product: acetaminofen, saleUnit: blister }), 'acetaminofen', 'blister', 2)

    const merged = changeUnit(cart, 'acetaminofen', 'box', 'blister')

    expect(merged).toHaveLength(1)
    expect(merged[0]).toMatchObject({ saleUnitId: 'blister', quantity: 3 })
    expect(estimatedTotal(merged)).toBe(13_500)
  })

  it('switching to a presentation not in the cart just changes the line and its price', () => {
    const cart = changeUnit(addToCart(emptyCart, { product: acetaminofen, saleUnit: box }), 'acetaminofen', 'box', 'blister')

    expect(cart[0].saleUnitId).toBe('blister')
    expect(estimatedTotal(cart)).toBe(4_500)
  })

  it('asks for the prescription only while a product needs it', () => {
    let cart = addToCart(emptyCart, { product: acetaminofen, saleUnit: box })
    expect(needsPrescription(cart)).toBe(false)

    cart = addToCart(cart, { product: amoxicilina, saleUnit: amoxicilina.saleUnits[0] })
    expect(needsPrescription(cart)).toBe(true)

    expect(needsPrescription(removeLine(cart, 'amoxicilina', 'amox-box'))).toBe(false)
  })
})

describe('summarizePayment (the same rules as the server)', () => {
  it('cash only: what is handed over beyond the total is the change', () => {
    const summary = summarizePayment(24_000, [], 50_000)

    expect(summary).toMatchObject({ cashDue: 24_000, change: 26_000, missing: 0, problem: null })
    expect(summary.payments).toEqual([{ method: 'Cash', amount: 50_000 }])
  })

  it('mixed: the card pays its part and cash covers the rest', () => {
    const summary = summarizePayment(24_000, [{ method: 'Card', amount: 10_000 }], 20_000)

    expect(summary).toMatchObject({ cashDue: 14_000, change: 6_000, problem: null })
    expect(summary.payments).toEqual([
      { method: 'Cash', amount: 20_000 },
      { method: 'Card', amount: 10_000 },
    ])
  })

  it('not enough cash: says how much is missing and does not let charge', () => {
    const summary = summarizePayment(24_000, [], 20_000)

    expect(summary.missing).toBe(4_000)
    expect(summary.change).toBe(0)
    expect(summary.problem).not.toBeNull()
  })

  it('when the other methods cover the total, no cash is sent (the server would reject it)', () => {
    const summary = summarizePayment(24_000, [{ method: 'DigitalWallet', amount: 24_000 }], 50_000)

    expect(summary.problem).toBeNull()
    expect(summary.change).toBe(0)
    expect(summary.payments).toEqual([{ method: 'DigitalWallet', amount: 24_000 }])
  })

  it('a transfer over the total is not allowed: nobody gives change on a transfer', () => {
    expect(summarizePayment(24_000, [{ method: 'BankTransfer', amount: 30_000 }], null).problem).not.toBeNull()
  })

  it('a typed amount that is empty, negative or not a number counts as nothing', () => {
    expect([toAmount('20000'), toAmount(' '), toAmount('-5'), toAmount('abc')]).toEqual([20_000, 0, 0, 0])
  })

  it('credit pays an exact amount, like a card: cash covers the rest', () => {
    const rosa = { available: 50_000, isSuspended: false }
    const summary = summarizePayment(24_000, [{ method: 'Credit', amount: 14_000 }], 10_000, rosa)

    expect(summary).toMatchObject({ cashDue: 10_000, change: 0, problem: null })
    expect(summary.payments).toEqual([
      { method: 'Cash', amount: 10_000 },
      { method: 'Credit', amount: 14_000 },
    ])
  })

  it('credit needs a customer, an account not suspended, and room in the limit', () => {
    const onCredit = (customer: { available: number; isSuspended: boolean } | null) =>
      summarizePayment(24_000, [{ method: 'Credit', amount: 24_000 }], null, customer).problem

    expect(onCredit(null)).toContain('cliente')
    expect(onCredit({ available: 50_000, isSuspended: true })).toContain('suspendido')
    expect(onCredit({ available: 20_000, isSuspended: false })).toContain('cupo')
    expect(onCredit({ available: 24_000, isSuspended: false })).toBeNull() // exactly up to the limit is fine
  })

  it('an empty cart cannot be charged', () => {
    expect(summarizePayment(0, [], 10_000).problem).not.toBeNull()
  })
})

describe('cart storage', () => {
  /** A Map-backed Storage, as the browser's localStorage behaves. */
  const fakeStorage = (): Storage => {
    const items = new Map<string, string>()
    return {
      get length() {
        return items.size
      },
      clear: () => items.clear(),
      getItem: (key) => items.get(key) ?? null,
      key: (index) => [...items.keys()][index] ?? null,
      removeItem: (key) => void items.delete(key),
      setItem: (key, value) => void items.set(key, value),
    }
  }
  const cart = addToCart(emptyCart, { product: acetaminofen, saleUnit: box })
  const now = Date.UTC(2026, 9, 29, 15, 0)

  it('the cart comes back after a reload, for the same user only', () => {
    const storage = fakeStorage()
    saveCart(storage, cartKey('pharmacy', 'ana'), cart, now)

    expect(loadCart(storage, cartKey('pharmacy', 'ana'), now + 60_000)).toEqual(cart)
    expect(loadCart(storage, cartKey('pharmacy', 'beto'), now + 60_000)).toEqual([])
  })

  it('a cart left from the day before is dropped', () => {
    const storage = fakeStorage()
    saveCart(storage, 'key', cart, now)

    expect(loadCart(storage, 'key', now + 13 * 60 * 60 * 1000)).toEqual([])
  })

  it('an empty cart removes the key, and unreadable data starts an empty cart instead of breaking the register', () => {
    const storage = fakeStorage()
    saveCart(storage, 'key', cart, now)
    saveCart(storage, 'key', emptyCart, now)
    expect(storage.getItem('key')).toBeNull()

    storage.setItem('key', '{not json')
    expect(loadCart(storage, 'key', now)).toEqual([])

    storage.setItem('key', JSON.stringify({ version: 1, savedAt: now, lines: [{ productId: 'x', quantity: -2 }] }))
    expect(loadCart(storage, 'key', now)).toEqual([])
  })
})
