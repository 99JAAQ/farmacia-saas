import { call, createPharmacy, createProductWithStock, createUser } from './api'
import { expect, loginInBrowser, test } from './fixtures'

test('una venta en la caja se ve en vivo en el catálogo y en el panel del día', async ({ page }) => {
  const pharmacy = await createPharmacy()
  const { productId, boxId, name } = await createProductWithStock(pharmacy, 10)
  const cashier = await createUser(pharmacy, 'Cashier')

  await loginInBrowser(page, pharmacy.ownerEmail, pharmacy.ownerPassword)
  await expect(page.getByRole('heading', { name: 'Hoy' })).toBeVisible()
  await page.getByRole('link', { name: 'Catálogo' }).first().click()
  const row = page.getByRole('row').filter({ hasText: name })
  await expect(row).toContainText('10 × Caja x30')

  // Another register sells 3 boxes (through the API, as the POS does).
  await call('POST', '/api/cash-sessions', cashier.token, { openingFloat: 0 })
  await call('POST', '/api/sales', cashier.token, {
    lines: [{ productId, saleUnitId: boxId, quantity: 3 }],
    payments: [{ method: 'Cash', amount: 36_000 }],
    prescriptionChecked: false,
  })

  // No reload: the owner's screen changes by itself (SignalR notice -> the query fetches again).
  await expect(row).toContainText('7 × Caja x30', { timeout: 10_000 })

  await page.getByRole('link', { name: 'Inicio' }).first().click()
  await expect(page.getByText('$ 36.000').first()).toBeVisible()
})

test('un producto que cruza su mínimo avisa a quien maneja el inventario', async ({ page }) => {
  const pharmacy = await createPharmacy()
  const { productId, boxId, name } = await createProductWithStock(pharmacy, 3)
  const keeper = await createUser(pharmacy, 'StockKeeper')
  const cashier = await createUser(pharmacy, 'Cashier')
  await call('PUT', `/api/products/${productId}/minimum-stock`, keeper.token, { minimumStock: 90 }) // 3 boxes: not below yet

  await loginInBrowser(page, keeper.email, 'Propia2026x')
  await expect(page.getByRole('heading', { name: /Hola/ })).toBeVisible()

  await call('POST', '/api/cash-sessions', cashier.token, { openingFloat: 0 })
  await call('POST', '/api/sales', cashier.token, {
    lines: [{ productId, saleUnitId: boxId, quantity: 1 }],
    payments: [{ method: 'Cash', amount: 12_000 }],
    prescriptionChecked: false,
  })

  const notice = page.getByRole('status').filter({ hasText: 'por debajo de su mínimo' })
  await expect(notice).toContainText(name, { timeout: 10_000 })
  await expect(page.getByRole('link', { name: /Alertas/ }).first()).toContainText('1')
})
