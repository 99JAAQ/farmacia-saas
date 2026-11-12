import type { Role, User } from '@/lib/api/types'

/**
 * Mirrors the API's named policies (src/Farmacia.Api/Authentication/Policies.cs).
 * Only for the user experience (hiding routes and buttons): the API is what actually enforces them.
 */
const policies: Record<'manageCatalog' | 'receivePurchases' | 'operateCash' | 'manageCredit' | 'manageInventory' | 'paySuppliers' | 'managePharmacy' | 'manageUsers' | 'managePlatform', readonly Role[]> = {
  manageCatalog: ['Owner', 'StockKeeper'],
  receivePurchases: ['Owner', 'StockKeeper'],
  operateCash: ['Owner', 'Cashier'],
  manageCredit: ['Owner'],
  manageInventory: ['Owner', 'StockKeeper'],
  paySuppliers: ['Owner'],
  managePharmacy: ['Owner'],
  manageUsers: ['Owner'],
  managePlatform: ['PlatformAdmin'],
}

export type Policy = keyof typeof policies

/** Like the API, every policy refuses a user who still has a temporary password. */
export function can(user: User | null, policy: Policy): boolean {
  return user !== null && !user.mustChangePassword && user.roles.some((role) => policies[policy].includes(role))
}
