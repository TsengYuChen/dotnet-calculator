// fetch wrapper (design §2.8): same-origin cookies, X-XSRF-TOKEN on writes, 401 → login page,
// CSRF_INVALID → fetch /api/me (which issues a new token cookie) and retry once.
import type { ProblemDetails } from './types'

export const XSRF_COOKIE = 'XSRF-TOKEN'
export const XSRF_HEADER = 'X-XSRF-TOKEN'

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails | null

  constructor(status: number, problem: ProblemDetails | null) {
    super(problem?.code ?? `HTTP ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }
}

export interface ClientDeps {
  fetch: (input: string, init: RequestInit) => Promise<Response>
  readCookie: (name: string) => string | undefined
  onUnauthorized: () => void
}

export interface ApiClient {
  get<T>(path: string): Promise<T>
  post<T>(path: string, body?: unknown): Promise<T>
  put<T>(path: string, body: unknown): Promise<T>
  patch<T>(path: string, body: unknown): Promise<T>
  delete<T>(path: string): Promise<T>
}

const SAFE_METHODS = new Set(['GET', 'HEAD'])

async function readBody(response: Response): Promise<unknown> {
  if (response.status === 204) return undefined
  const text = await response.text()
  if (text === '') return undefined
  const type = response.headers.get('Content-Type') ?? ''
  return type.includes('json') ? JSON.parse(text) : text
}

export function createApiClient(deps: ClientDeps): ApiClient {
  async function send<T>(method: string, path: string, body: unknown, retried: boolean): Promise<T> {
    const headers = new Headers({ Accept: 'application/json' })
    if (body !== undefined) headers.set('Content-Type', 'application/json')
    if (!SAFE_METHODS.has(method)) {
      const token = deps.readCookie(XSRF_COOKIE)
      if (token) headers.set(XSRF_HEADER, token)
    }

    const response = await deps.fetch(path, {
      method,
      headers,
      credentials: 'same-origin',
      body: body === undefined ? undefined : JSON.stringify(body),
    })
    const data = await readBody(response).catch(() => undefined)

    if (response.ok) return data as T

    const problem = typeof data === 'object' && data !== null ? (data as ProblemDetails) : null
    if (response.status === 401) {
      deps.onUnauthorized()
      throw new ApiError(401, problem)
    }
    if (problem?.code === 'CSRF_INVALID' && !retried) {
      // The token is bound to the signed-in user; /api/me issues a fresh XSRF-TOKEN cookie.
      await send<unknown>('GET', '/api/me', undefined, true)
      return send<T>(method, path, body, true)
    }
    throw new ApiError(response.status, problem)
  }

  return {
    get: (path) => send('GET', path, undefined, false),
    post: (path, body) => send('POST', path, body, false),
    put: (path, body) => send('PUT', path, body, false),
    patch: (path, body) => send('PATCH', path, body, false),
    delete: (path) => send('DELETE', path, undefined, false),
  }
}

export function readCookieFrom(cookieString: string, name: string): string | undefined {
  for (const pair of cookieString.split(';')) {
    const index = pair.indexOf('=')
    if (index < 0) continue
    if (pair.slice(0, index).trim() === name) return decodeURIComponent(pair.slice(index + 1).trim())
  }
  return undefined
}

let unauthorizedHandler: () => void = () => {}

/** main.ts wires this to the router (navigate to /login). */
export function setUnauthorizedHandler(handler: () => void): void {
  unauthorizedHandler = handler
}

/** The app's client, bound to the browser. */
export const api: ApiClient = createApiClient({
  fetch: (input, init) => globalThis.fetch(input, init),
  readCookie: (name) => (typeof document === 'undefined' ? undefined : readCookieFrom(document.cookie, name)),
  onUnauthorized: () => unauthorizedHandler(),
})
