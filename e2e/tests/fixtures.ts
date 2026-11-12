import { test as base, expect, type Page } from '@playwright/test'

/**
 * Every test fails if the browser logs an error or a warning: a script blocked by the CSP, a module served as
 * HTML, a React warning. The only expected one is the 401 of the session check on the login page.
 */
export const test = base.extend<{ consoleProblems: string[] }>({
  consoleProblems: [
    async ({ context }, use) => {
      const problems: string[] = []
      context.on('console', (message) => {
        if (!['error', 'warning'].includes(message.type())) return
        if (message.text().includes('401')) return
        problems.push(message.text())
      })
      context.on('weberror', (error) => problems.push(error.error().message))
      await use(problems)
      expect(problems, 'errores o avisos en la consola del navegador').toEqual([])
    },
    { auto: true },
  ],
})

export { expect }

export async function loginInBrowser(page: Page, email: string, password: string) {
  await page.goto('/login')
  await page.getByLabel('Correo').fill(email)
  await page.getByLabel('Contraseña').fill(password)
  await page.getByRole('button', { name: 'Ingresar' }).click()
}

export async function changeTemporaryPassword(page: Page, temporary: string, newPassword: string) {
  await expect(page).toHaveURL(/\/cambiar-clave$/)
  await page.getByLabel('Contraseña temporal').fill(temporary)
  await page.getByLabel('Contraseña nueva').fill(newPassword)
  await page.getByLabel('Repita la nueva').fill(newPassword)
  await page.getByRole('button', { name: 'Guardar contraseña' }).click()
  await expect(page.getByRole('heading', { name: /Hola/ })).toBeVisible()
}
