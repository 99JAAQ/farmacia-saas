import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { api, ApiError, onSessionChange, setSession } from './http'
import type { Session } from './types'

type Handler = (path: string, init: RequestInit) => Response | Promise<Response>

function mockFetch(handler: Handler) {
  const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) =>
    Promise.resolve(handler(String(input), init ?? {})),
  )
  vi.stubGlobal('fetch', fetchMock)
  return fetchMock
}

const json = (status: number, body?: unknown) =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })

const delayed = (ms: number, response: () => Response) =>
  new Promise<Response>((resolve) => setTimeout(() => resolve(response()), ms))

const bearer = (init: RequestInit) => new Headers(init.headers).get('Authorization')

const session = (accessToken: string): Session => ({
  accessToken,
  accessTokenExpiresAt: '2026-10-23T12:00:00Z',
  user: { id: 'u1', email: 'cajero@farmacia.dev', fullName: 'Cajero', pharmacyId: 'p1', roles: ['Cashier'], mustChangePassword: false },
})

const callsTo = (fetchMock: ReturnType<typeof mockFetch>, path: string) =>
  fetchMock.mock.calls.filter(([input]) => String(input) === path).length

describe('api', () => {
  beforeEach(() => setSession(null))
  afterEach(() => vi.unstubAllGlobals())

  it('an answer without a body is fine with any success status, not only 204', async () => {
    mockFetch(() => json(201))

    await expect(api<void>('/api/customers/1/credit', { method: 'POST' })).resolves.toBeUndefined()
  })

  it('sends the access token as a Bearer header', async () => {
    setSession(session('token-1'))
    const fetchMock = mockFetch(() => json(200, { ok: true }))

    await api('/api/products/1')

    expect(bearer(fetchMock.mock.calls[0][1]!)).toBe('Bearer token-1')
  })

  it('on a 401 renews the session once and retries with the new token', async () => {
    setSession(session('expired'))
    const fetchMock = mockFetch((path, init) => {
      if (path === '/api/auth/refresh') return json(200, session('fresh'))
      return bearer(init) === 'Bearer fresh' ? json(200, { name: 'Acetaminofén' }) : json(401)
    })

    const result = await api<{ name: string }>('/api/products/1')

    expect(result.name).toBe('Acetaminofén')
    expect(callsTo(fetchMock, '/api/auth/refresh')).toBe(1)
  })

  it('ten simultaneous 401s share a single refresh (the API would treat a second one as theft)', async () => {
    setSession(session('expired'))
    const fetchMock = mockFetch((path, init) => {
      if (path === '/api/auth/refresh') return delayed(20, () => json(200, session('fresh')))
      return bearer(init) === 'Bearer fresh' ? json(200, { ok: true }) : json(401)
    })

    const results = await Promise.all(Array.from({ length: 10 }, (_, i) => api(`/api/products/${i}`)))

    expect(results).toHaveLength(10)
    expect(callsTo(fetchMock, '/api/auth/refresh')).toBe(1)
  })

  it('a 401 that arrives after another request renewed the token retries without a new refresh', async () => {
    setSession(session('old'))
    const fetchMock = mockFetch((path, init) => {
      if (path === '/api/auth/refresh') return json(200, session('unexpected'))
      return bearer(init) === 'Bearer old' ? delayed(30, () => json(401)) : json(200, { ok: true })
    })

    const pending = api('/api/products/1')
    setSession(session('new')) // another request renewed the session meanwhile
    await pending

    expect(callsTo(fetchMock, '/api/auth/refresh')).toBe(0)
    expect(bearer(fetchMock.mock.calls.at(-1)![1]!)).toBe('Bearer new')
  })

  it('when the refresh is rejected, ends the session and throws the 401', async () => {
    setSession(session('expired'))
    mockFetch(() => json(401, { title: 'No autorizado', detail: 'La sesión no es válida o expiró.' }))
    const sessions: (Session | null)[] = []
    const unsubscribe = onSessionChange((s) => sessions.push(s))

    const error = await api('/api/products/1').catch((e: unknown) => e)

    unsubscribe()
    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(401)
    expect(sessions).toEqual([null])
  })

  it('skipRefresh: a 401 from the login is a final answer, not an expired token', async () => {
    const fetchMock = mockFetch(() => json(401, { detail: 'Correo o contraseña incorrectos.' }))

    const error = await api('/api/auth/login', { method: 'POST', body: {}, skipRefresh: true }).catch((e: unknown) => e)

    expect((error as ApiError).message).toBe('Correo o contraseña incorrectos.')
    expect(callsTo(fetchMock, '/api/auth/refresh')).toBe(0)
  })

  it('a network error does not end the session (offline is not logged out)', async () => {
    setSession(session('token-1'))
    vi.stubGlobal('fetch', vi.fn(() => Promise.reject(new TypeError('Failed to fetch'))))
    const sessions: (Session | null)[] = []
    const unsubscribe = onSessionChange((s) => sessions.push(s))

    const error = await api('/api/products/1').catch((e: unknown) => e)

    unsubscribe()
    expect((error as ApiError).isNetworkError).toBe(true)
    expect(sessions).toEqual([])

    // When the connection comes back, the same token is still used.
    const fetchMock = mockFetch(() => json(200, {}))
    await api('/api/products/1')
    expect(bearer(fetchMock.mock.calls[0][1]!)).toBe('Bearer token-1')
  })

  it('uses the ProblemDetails "detail" as the error message', async () => {
    setSession(session('token-1'))
    mockFetch(() => json(409, { title: 'Conflicto', detail: 'Códigos de barras ya asignados a otro producto.' }))

    const error = await api('/api/products', { method: 'POST', body: {} }).catch((e: unknown) => e)

    expect((error as ApiError).status).toBe(409)
    expect((error as ApiError).message).toBe('Códigos de barras ya asignados a otro producto.')
  })
})
