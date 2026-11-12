import { z } from 'zod'

/**
 * The same rules as Identity's password options in the API (DependencyInjection.cs): at least 8 characters, an
 * uppercase letter, a lowercase letter and a number, no symbol required. Only to warn before sending: the API checks.
 */
export function passwordProblem(password: string): string | null {
  if (password.length < 8) return 'Al menos 8 caracteres.'
  if (!/[A-ZÁÉÍÓÚÑ]/.test(password)) return 'Al menos una mayúscula.'
  if (!/[a-záéíóúñ]/.test(password)) return 'Al menos una minúscula.'
  if (!/\d/.test(password)) return 'Al menos un número.'
  return null
}

/** For react-hook-form + zod schemas. */
export const passwordField = z.string().superRefine((value, context) => {
  const problem = passwordProblem(value)
  if (problem !== null) context.addIssue({ code: 'custom', message: problem })
})

export const passwordHint = 'Mínimo 8 caracteres, con mayúscula, minúscula y número.'
