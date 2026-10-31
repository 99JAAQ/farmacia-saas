import type { ProblemDetails, Session } from './types'

/**
 * Minimal HTTP client for the API.
 * - The access token lives only in this module's memory, never in localStorage, where an XSS script could read it.
 * - On a 401 it renews the session once with the HttpOnly refresh cookie and retries the request.
 * - Concurrent 401s share a single refresh call: the API treats a refresh token used twice as stolen.
 * - A network error never ends the session: with unstable internet, "offline" is not "logged out".
 */

export class ApiError extends Error {
  readonly status: number
  readonly problem?: ProblemDetails

  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  /** 0 means the request never reached the server. */
  get isNetworkError(): boolean {
    return this.status === 0
  }
}

type SessionListener = (session: Session | null) => void

let accessToken: string | null = null
let refreshInFlight: Promise<Session | null> | null = null
const listeners = new Set<SessionListener>()

/** Notifies every session change (login, refresh, logout, expiration). Returns the unsubscribe function. */
export function onSessionChange(listener: SessionListener): () => void {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

export function setSession(session: Session | null): void {
  accessToken = session?.accessToken ?? null
  listeners.forEach((listener) => listener(session))
}

/**
 * Exchanges the refresh cookie for a new session. Callers that arrive while a refresh is running get the same
 * promise instead of starting another one. Resolves to null when the session is over (the API answered 401).
 */
export function refreshSession(): Promise<Session | null> {
  refreshInFlight ??= (async () => {
    try {
      const response = await send('/api/auth/refresh', { method: 'POST' })
      const session = response.ok ? ((await response.json()) as Session) : null
      setSession(session)
      return session
    } finally {
      refreshInFlight = null
    }
  })()

  return refreshInFlight
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  body?: unknown
  signal?: AbortSignal
  /** For the auth endpoints: a 401 there is a final answer, not an expired access token. */
  skipRefresh?: boolean
}

export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const tokenUsed = accessToken
  let response = await send(path, options)

  if (response.status === 401 && !options.skipRefresh) {
    // If another request already renewed the token while this one was in flight, just retry with the new one.
    const renewed = accessToken !== null && accessToken !== tokenUsed
    if (renewed || (await refreshSession())) response = await send(path, options)
  }

  if (!response.ok) throw await toApiError(response)

  // No body is a valid answer, not only with 204: e.g. a 201 that only sends a Location header.
  const text = await response.text()
  return (text === '' ? undefined : JSON.parse(text)) as T
}

async function send(path: string, { method = 'GET', body, signal }: RequestOptions): Promise<Response> {
  const headers = new Headers({ Accept: 'application/json' })
  if (body !== undefined) headers.set('Content-Type', 'application/json')
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)

  try {
    // Same origin (Vite proxy in development), so the browser sends the refresh cookie on its own.
    return await fetch(path, {
      method,
      headers,
      signal,
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new ApiError(0, 'No se pudo conectar con el servidor. Revise la conexión a internet.')
  }
}

async function toApiError(response: Response): Promise<ApiError> {
  const problem = (await response.json().catch(() => undefined)) as ProblemDetails | undefined
  return new ApiError(response.status, problem?.detail ?? problem?.title ?? defaultMessage(response.status), problem)
}

function defaultMessage(status: number): string {
  switch (status) {
    case 401:
      return 'Su sesión terminó. Inicie sesión de nuevo.'
    case 403:
      return 'No tiene permiso para hacer esta operación.'
    default:
      return 'Ocurrió un error inesperado. Intente de nuevo.'
  }
}
