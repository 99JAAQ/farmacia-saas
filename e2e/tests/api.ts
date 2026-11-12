/**
 * Prepares data through the public API, as a real client would: the tests check the screens, not the setup.
 * No backdoor into the database: what a test can do here, a user can do too.
 */
export const baseUrl = process.env.E2E_BASE_URL ?? 'http://localhost:8080'

export const admin = {
  email: process.env.E2E_ADMIN_EMAIL ?? 'admin@farmacia.local',
  bootstrapPassword: process.env.E2E_ADMIN_BOOTSTRAP_PASSWORD ?? 'Inicial2026',
  password: process.env.E2E_ADMIN_PASSWORD ?? 'Plataforma2026',
}

export async function call<T>(method: string, path: string, token?: string, body?: unknown): Promise<T> {
  const response = await fetch(baseUrl + path, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  const text = await response.text()
  if (!response.ok) throw new Error(`${method} ${path} -> ${response.status} ${text}`)
  return (text ? JSON.parse(text) : undefined) as T
}

interface Session {
  accessToken: string
  user: { mustChangePassword: boolean }
}

export async function login(email: string, password: string): Promise<Session> {
  return call<Session>('POST', '/api/auth/login', undefined, { email, password })
}

/** Logs in and, if the password is temporary, replaces it: returns a token that can work. */
export async function loginReady(email: string, password: string, newPassword: string): Promise<string> {
  const session = await login(email, password)
  if (!session.user.mustChangePassword) return session.accessToken
  const changed = await call<Session>('POST', '/api/auth/change-password', session.accessToken, { currentPassword: password, newPassword })
  return changed.accessToken
}

export async function adminToken(): Promise<string> {
  return (await login(admin.email, admin.password)).accessToken
}

const unique = () => `${Date.now()}${Math.floor(Math.random() * 1000)}`

/** A valid NIT (DIAN verification digit, module 11), different each time. */
export function uniqueNit(): string {
  const number = String(Math.floor(100_000_000 + Math.random() * 899_999_999))
  const weights = [41, 37, 29, 23, 19, 17, 13, 7, 3]
  const remainder = [...number].reduce((sum, digit, i) => sum + Number(digit) * weights[i], 0) % 11
  return `${number}-${remainder < 2 ? remainder : 11 - remainder}`
}

export interface Pharmacy {
  name: string
  ownerEmail: string
  ownerPassword: string
  ownerToken: string
}

/** A new pharmacy whose owner already changed the temporary password. */
export async function createPharmacy(): Promise<Pharmacy> {
  const id = unique()
  const pharmacy = { name: `Droguería E2E ${id}`, ownerEmail: `dueno-${id}@e2e.test`, ownerPassword: 'Duena2026x' }
  await call('POST', '/api/platform/pharmacies', await adminToken(), {
    name: pharmacy.name,
    taxId: uniqueNit(),
    address: null,
    phone: null,
    isVatResponsible: true,
    ownerFullName: 'Dueña de Prueba',
    ownerEmail: pharmacy.ownerEmail,
    ownerPassword: 'Temporal2026',
  })
  const ownerToken = await loginReady(pharmacy.ownerEmail, 'Temporal2026', pharmacy.ownerPassword)
  return { ...pharmacy, ownerToken }
}

/** A user of the pharmacy, created by its owner, who already changed the temporary password. */
export async function createUser(pharmacy: Pharmacy, role: 'Cashier' | 'StockKeeper'): Promise<{ email: string; token: string }> {
  const email = `${role.toLowerCase()}-${unique()}@e2e.test`
  await call('POST', '/api/users', pharmacy.ownerToken, { fullName: `Usuario ${role}`, email, role, temporaryPassword: 'Temporal2026' })
  return { email, token: await loginReady(email, 'Temporal2026', 'Propia2026x') }
}

/**
 * A product sold by boxes of 30 at 12.000, with stock received the only way stock enters: a confirmed purchase.
 * Returns its Id and the box's.
 */
export async function createProductWithStock(pharmacy: Pharmacy, boxes: number): Promise<{ productId: string; boxId: string; name: string }> {
  const token = pharmacy.ownerToken
  const name = `Acetaminofén E2E ${unique()}`
  const { id: productId } = await call<{ id: string }>('POST', '/api/products', token, {
    name,
    baseUnitName: 'Tableta',
    requiresPrescription: false,
    activeIngredient: null,
    concentration: null,
    pharmaceuticalForm: null,
    sanitaryRegistration: null,
    categoryId: null,
    saleUnits: [{ name: 'Caja x30', conversionFactor: 30, price: 12_000, barcode: null }],
  })
  const product = await call<{ saleUnits: { id: string }[] }>('GET', `/api/products/${productId}`, token)
  const boxId = product.saleUnits[0].id

  const { id: supplierId } = await call<{ id: string }>('POST', '/api/suppliers', token, {
    name: 'Distribuidora E2E', taxId: uniqueNit(), contactName: null, phone: null, email: null,
  })
  const yesterday = new Date(Date.now() - 86_400_000).toISOString().slice(0, 10)
  const { id: purchaseId } = await call<{ id: string }>('POST', '/api/purchases', token, {
    supplierId, invoiceNumber: `FE-${unique()}`, invoiceDate: yesterday, paymentTerm: 'Cash', dueDate: null,
  })
  await call('POST', `/api/purchases/${purchaseId}/items`, token, {
    productId, saleUnitId: boxId, quantity: boxes, bonusQuantity: 0, unitCost: 6_000, lineDiscount: 0,
    vatRate: 'Exempt', lotNumber: 'L-E2E', expirationDate: '2030-12-31',
  })
  await call('POST', `/api/purchases/${purchaseId}/confirm`, token, { printedInvoiceTotal: boxes * 6_000 })
  return { productId, boxId, name }
}
