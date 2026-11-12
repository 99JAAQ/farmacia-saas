import { describe, expect, it } from 'vitest'
import { describeStatus, type UserAccount } from './api'

const user: UserAccount = {
  id: '1',
  fullName: 'Carlos Ruiz',
  email: 'carlos@drogueria.co',
  role: 'Cashier',
  isActive: true,
  mustChangePassword: false,
}

describe('user status', () => {
  it('says whether the user can work, and warns while the temporary password is unchanged', () => {
    expect(describeStatus(user)).toEqual({ text: 'Activo', tone: 'success' })
    expect(describeStatus({ ...user, mustChangePassword: true }).tone).toBe('warning')
    // Deactivated wins: a temporary password does not matter to someone who cannot log in.
    expect(describeStatus({ ...user, isActive: false, mustChangePassword: true })).toEqual({ text: 'Desactivado', tone: 'muted' })
  })
})
