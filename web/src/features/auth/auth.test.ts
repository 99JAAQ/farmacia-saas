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
  const userWith = (...roles: Role[]): User => ({ id: 'u', email: 'u@f.dev', fullName: 'U', pharmacyId: 'p', roles })

  it('lets the owner and the stock keeper manage the catalog', () => {
    expect(can(userWith('Owner'), 'manageCatalog')).toBe(true)
    expect(can(userWith('StockKeeper'), 'manageCatalog')).toBe(true)
  })

  it('does not let the cashier or an anonymous visitor manage the catalog', () => {
    expect(can(userWith('Cashier'), 'manageCatalog')).toBe(false)
    expect(can(null, 'manageCatalog')).toBe(false)
  })
})
