import { useEffect, useState } from 'react'
import type { User } from '@/lib/api/types'
import type { Cart } from './cart'
import { cartKey, loadCart, saveCart } from './cartStorage'

/** The cart, saved on every change so a reload or a power cut does not lose it (see cartStorage.ts). */
export function useCart(user: User) {
  // Only pharmacy users reach the POS (operateCash); the administrator has no pharmacy and no cart.
  const key = cartKey(user.pharmacyId ?? 'none', user.id)
  const [cart, setCart] = useState<Cart>(() => loadCart(localStorage, key))

  useEffect(() => saveCart(localStorage, key, cart), [key, cart])

  return [cart, setCart] as const
}
