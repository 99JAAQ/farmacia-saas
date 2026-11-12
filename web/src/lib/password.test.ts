import { describe, expect, it } from 'vitest'
import { passwordProblem } from './password'

describe('password rules (same as the API)', () => {
  it('accepts 8+ characters with an uppercase letter, a lowercase letter and a number, without symbols', () => {
    expect(passwordProblem('Farmacia2026')).toBeNull()
    expect(passwordProblem('Ñandú2026')).toBeNull()
  })

  it('says what is missing', () => {
    expect(passwordProblem('Corta1')).toContain('8 caracteres')
    expect(passwordProblem('farmacia2026')).toContain('mayúscula')
    expect(passwordProblem('FARMACIA2026')).toContain('minúscula')
    expect(passwordProblem('Farmacias')).toContain('número')
  })
})
