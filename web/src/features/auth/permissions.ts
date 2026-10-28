import type { Role, User } from '@/lib/api/types'

/**
 * Mirrors the API's named policies (src/Farmacia.Api/Authentication/Policies.cs).
 * Only for the user experience (hiding routes and buttons): the API is what actually enforces them.
 */
const policies: Record<'manageCatalog' | 'receivePurchases' | 'operateCash', readonly Role[]> = {
  manageCatalog: ['Owner', 'StockKeeper'],
  receivePurchases: ['Owner', 'StockKeeper'],
  operateCash: ['Owner', 'Cashier'],
}

export type Policy = keyof typeof policies

export function can(user: User | null, policy: Policy): boolean {
  return user !== null && user.roles.some((role) => policies[policy].includes(role))
}
