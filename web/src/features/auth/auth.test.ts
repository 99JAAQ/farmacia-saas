import { describe, expect, it } from 'vitest'
import type { Role, User } from '@/lib/api/types'
import { can } from './permissions'
import { safeRedirectPath } from './redirect'

describe('safeRedirectPath (open redirect protection)', () => {
  it.each(['/', '/catalogo', '/catalogo?pagina=2'])('accepts the internal path %s', (path) => {
    expect(safeRedirectPath(path)).toBe(path)
  })

  it.each(['https://sitio-falso.com', '//sitio-falso.com', '/\\sitio-falso.com', 'javascript:alert(1)', '/login', undefined, 42])(
    'rejects %s and goes home',
    (candidate) => {
      expect(safeRedirectPath(candidate)).toBe('/')
    },
  )
})

describe('can (mirror of the API policies)', () => {
  const userWith = (...roles: Role[]): User => ({ id: 'u', email: 'u@f.dev', fullName: 'U', pharmacyId: 'p', roles, mustChangePassword: false })

  it('lets the owner and the stock keeper manage the catalog', () => {
    expect(can(userWith('Owner'), 'manageCatalog')).toBe(true)
    expect(can(userWith('StockKeeper'), 'manageCatalog')).toBe(true)
  })

  it('lets only the owner and the stock keeper receive purchases (they show costs)', () => {
    expect(can(userWith('Owner'), 'receivePurchases')).toBe(true)
    expect(can(userWith('StockKeeper'), 'receivePurchases')).toBe(true)
    expect(can(userWith('Cashier'), 'receivePurchases')).toBe(false)
  })

  it('lets the owner and the cashier operate the register, but not the stock keeper', () => {
    expect(can(userWith('Owner'), 'operateCash')).toBe(true)
    expect(can(userWith('Cashier'), 'operateCash')).toBe(true)
    expect(can(userWith('StockKeeper'), 'operateCash')).toBe(false)
  })

  it('lets only the owner give credit: the cashier sells on credit but never decides who gets it', () => {
    expect(can(userWith('Owner'), 'manageCredit')).toBe(true)
    expect(can(userWith('Cashier'), 'manageCredit')).toBe(false)
    expect(can(userWith('StockKeeper'), 'manageCredit')).toBe(false)
  })

  it('lets the owner and the stock keeper write off and adjust stock, but not the cashier', () => {
    expect(can(userWith('Owner'), 'manageInventory')).toBe(true)
    expect(can(userWith('StockKeeper'), 'manageInventory')).toBe(true)
    expect(can(userWith('Cashier'), 'manageInventory')).toBe(false)
  })

  it('lets only the owner pay suppliers: the stock keeper receives purchases but does not pay them', () => {
    expect(can(userWith('Owner'), 'paySuppliers')).toBe(true)
    expect(can(userWith('StockKeeper'), 'paySuppliers')).toBe(false)
    expect(can(userWith('Cashier'), 'paySuppliers')).toBe(false)
  })

  it('lets only the owner manage users, and only the platform administrator manage pharmacies', () => {
    expect(can(userWith('Owner'), 'manageUsers')).toBe(true)
    expect(can(userWith('Cashier'), 'manageUsers')).toBe(false)
    expect(can(userWith('PlatformAdmin'), 'managePlatform')).toBe(true)
    expect(can(userWith('Owner'), 'managePlatform')).toBe(false)
  })

  it('allows nothing while the user has a temporary password, like the API', () => {
    const owner = { ...userWith('Owner'), mustChangePassword: true }
    for (const policy of ['manageUsers', 'operateCash', 'manageCatalog', 'managePharmacy'] as const) expect(can(owner, policy)).toBe(false)
  })

  it('lets only the owner edit the pharmacy, and keeps the platform administrator out of every pharmacy screen', () => {
    expect(can(userWith('Owner'), 'managePharmacy')).toBe(true)
    expect(can(userWith('Cashier'), 'managePharmacy')).toBe(false)
    for (const policy of ['managePharmacy', 'operateCash', 'manageCatalog', 'receivePurchases'] as const)
      expect(can(userWith('PlatformAdmin'), policy)).toBe(false)
  })

  it('does not let the cashier or an anonymous visitor manage the catalog', () => {
    expect(can(userWith('Cashier'), 'manageCatalog')).toBe(false)
    expect(can(null, 'manageCatalog')).toBe(false)
  })
})
