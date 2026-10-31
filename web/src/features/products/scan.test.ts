import { describe, expect, it } from 'vitest'
import type { Product } from './api'
import { findScannedUnit } from './scan'

const product = (name: string, units: { name: string; factor: number; barcode: string | null }[]): Product => ({
  id: name,
  name,
  baseUnitName: 'Tableta',
  requiresPrescription: false,
  activeIngredient: null,
  concentration: null,
  pharmaceuticalForm: null,
  sanitaryRegistration: null,
  categoryId: null,
  vatRate: 'Exempt',
  isActive: true,
  saleUnits: units.map((u) => ({ id: `${name}-${u.name}`, name: u.name, conversionFactor: u.factor, price: 1000, barcode: u.barcode })),
})

const acetaminofen = product('Acetaminofén', [
  { name: 'Caja x30', factor: 30, barcode: '7702057000017' },
  { name: 'Blíster', factor: 10, barcode: '7702057000024' },
  { name: 'Tableta', factor: 1, barcode: null },
])
const ibuprofeno = product('Ibuprofeno', [{ name: 'Caja x20', factor: 20, barcode: '7702057000031' }])

describe('findScannedUnit', () => {
  it('a scanned barcode selects that exact presentation, not the first one', () => {
    const selection = findScannedUnit([acetaminofen], ' 7702057000024 ')

    expect(selection?.product.name).toBe('Acetaminofén')
    expect(selection?.saleUnit.name).toBe('Blíster')
  })

  it('a single product found by name selects its largest presentation', () => {
    expect(findScannedUnit([acetaminofen], 'aceta')?.saleUnit.name).toBe('Caja x30')
  })

  it('several products and no barcode: the user must choose', () => {
    expect(findScannedUnit([acetaminofen, ibuprofeno], 'caja')).toBeNull()
  })

  it('nothing typed or nothing found: no selection', () => {
    expect(findScannedUnit([acetaminofen], '   ')).toBeNull()
    expect(findScannedUnit([], '7702057000017')).toBeNull()
  })
})
