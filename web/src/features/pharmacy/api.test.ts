import { describe, expect, it } from 'vitest'
import { headerLines, type Pharmacy } from './api'

const pharmacy: Pharmacy = {
  id: '1',
  name: 'Droguería La Esperanza',
  taxId: '900123456-8',
  address: 'Calle 10 # 5-20',
  phone: '604 555 1234',
  isVatResponsible: true,
}

describe('ticket header', () => {
  it('prints the name, the NIT, the address and the phone', () => {
    expect(headerLines(pharmacy)).toEqual(['Droguería La Esperanza', 'NIT 900123456-8', 'Calle 10 # 5-20', 'Tel. 604 555 1234'])
  })

  it('leaves out what the pharmacy has not filled in', () => {
    expect(headerLines({ ...pharmacy, address: null, phone: null })).toEqual(['Droguería La Esperanza', 'NIT 900123456-8'])
  })

  it('says just "Farmacia" while the data has not loaded', () => {
    expect(headerLines(undefined)).toEqual(['Farmacia'])
  })
})
