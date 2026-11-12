import { admin, call, createPharmacy, uniqueNit } from './api'
import { changeTemporaryPassword, expect, loginInBrowser, test } from './fixtures'

test('el administrador crea una farmacia y su dueña empieza a trabajar', async ({ browser }) => {
  const id = Date.now()
  const ownerEmail = `marta-${id}@e2e.test`
  const nitNumber = uniqueNit().split('-')[0]

  const adminPage = await (await browser.newContext()).newPage()
  const response = await adminPage.goto('/login')
  expect(response?.headers()['content-security-policy']).toContain("default-src 'self'")
  await loginInBrowser(adminPage, admin.email, admin.password)
  await adminPage.getByRole('link', { name: 'Farmacias' }).first().click()
  await adminPage.getByRole('button', { name: 'Nueva farmacia' }).click()
  await adminPage.getByLabel('Nombre de la farmacia').fill(`Droguería La Esperanza ${id}`)
  await adminPage.getByLabel('NIT (sin dígito de verificación)').fill(nitNumber)
  await adminPage.getByLabel('Nombre', { exact: true }).fill('Marta Gómez')
  await adminPage.getByLabel('Correo').fill(ownerEmail)
  await adminPage.getByLabel('Contraseña temporal').fill('Temporal2026')
  await adminPage.getByRole('button', { name: 'Crear farmacia' }).click()
  await expect(adminPage.getByText(`Droguería La Esperanza ${id}`)).toBeVisible()

  // The owner must change the password the administrator knows before anything else.
  const owner = await (await browser.newContext()).newPage()
  await loginInBrowser(owner, ownerEmail, 'Temporal2026')
  await changeTemporaryPassword(owner, 'Temporal2026', 'Esperanza2026')
  await expect(owner.getByRole('heading', { name: 'Hoy' })).toBeVisible()
  await expect(owner.getByText(`Droguería La Esperanza ${id}`)).toBeVisible() // her pharmacy, under the logo

  // A reload on a lazily loaded section works (the API serves index.html for the app's routes).
  await owner.goto('/catalogo')
  await owner.reload()
  await expect(owner.getByRole('heading', { name: 'Catálogo' })).toBeVisible()
})

test('un usuario con contraseña temporal no puede usar nada hasta cambiarla', async ({ page }) => {
  const pharmacy = await createPharmacy()
  const email = `cajero-${Date.now()}@e2e.test`
  await call('POST', '/api/users', pharmacy.ownerToken, { fullName: 'Carlos Ruiz', email, role: 'Cashier', temporaryPassword: 'Temporal2026' })

  await loginInBrowser(page, email, 'Temporal2026')
  await expect(page).toHaveURL(/\/cambiar-clave$/)
  await page.goto('/caja') // trying to skip it
  await expect(page).toHaveURL(/\/cambiar-clave$/)

  await changeTemporaryPassword(page, 'Temporal2026', 'Carlos2026x')
  await page.getByRole('link', { name: 'Caja' }).first().click()
  await expect(page.getByRole('heading', { name: 'Caja' })).toBeVisible()
})
