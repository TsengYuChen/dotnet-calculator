import { describe, expect, it, vi } from 'vitest'
import { ApiError, createApiClient, readCookieFrom } from './client'

function json(status: number, body: unknown, contentType = 'application/json'): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': contentType } })
}

function setup(responses: Response[], cookies: string[] = ['token-1']) {
  const calls: { url: string; init: RequestInit }[] = []
  let cookieIndex = 0
  const fetch = vi.fn(async (url: string, init: RequestInit) => {
    calls.push({ url, init })
    if (url === '/api/me') cookieIndex = Math.min(cookieIndex + 1, cookies.length - 1)
    const next = responses.shift()
    if (!next) throw new Error(`unexpected request ${url}`)
    return next
  })
  const onUnauthorized = vi.fn()
  const client = createApiClient({ fetch, readCookie: () => cookies[cookieIndex], onUnauthorized })
  const header = (i: number) => new Headers(calls[i]!.init.headers).get('X-XSRF-TOKEN')
  return { client, calls, onUnauthorized, header }
}

describe('api client', () => {
  it('sends credentials, JSON and the XSRF token on writes', async () => {
    const { client, calls, header } = setup([json(201, { id: 'v1' })])
    await expect(client.post('/api/vehicles', { plate: 'ABC-1234' })).resolves.toEqual({ id: 'v1' })
    expect(calls[0]!.init.method).toBe('POST')
    expect(calls[0]!.init.credentials).toBe('same-origin')
    expect(calls[0]!.init.body).toBe('{"plate":"ABC-1234"}')
    expect(new Headers(calls[0]!.init.headers).get('Content-Type')).toBe('application/json')
    expect(header(0)).toBe('token-1')
  })

  it('does not send the XSRF token on GET', async () => {
    const { client, header } = setup([json(200, [])])
    await client.get('/api/vehicles')
    expect(header(0)).toBeNull()
  })

  it('returns undefined for 204', async () => {
    const { client } = setup([new Response(null, { status: 204 })])
    await expect(client.post('/api/vehicles/v1/trash')).resolves.toBeUndefined()
  })

  it('on 401 calls onUnauthorized and throws', async () => {
    const { client, onUnauthorized } = setup([json(401, { status: 401 }, 'application/problem+json')])
    await expect(client.get('/api/vehicles')).rejects.toMatchObject({ status: 401 })
    expect(onUnauthorized).toHaveBeenCalledOnce()
  })

  it('on CSRF_INVALID refetches /api/me and retries once with the new token', async () => {
    const { client, calls, header } = setup(
      [
        json(400, { status: 400, code: 'CSRF_INVALID' }, 'application/problem+json'),
        json(200, { email: 'user@example.com', calendar: 'ROC' }),
        new Response(null, { status: 204 }),
      ],
      ['stale', 'fresh'],
    )
    await expect(client.delete('/api/vehicles/v1')).resolves.toBeUndefined()
    expect(calls.map((c) => `${c.init.method} ${c.url}`)).toEqual([
      'DELETE /api/vehicles/v1',
      'GET /api/me',
      'DELETE /api/vehicles/v1',
    ])
    expect(header(0)).toBe('stale')
    expect(header(2)).toBe('fresh')
  })

  it('retries CSRF_INVALID only once', async () => {
    const csrf = () => json(400, { status: 400, code: 'CSRF_INVALID' }, 'application/problem+json')
    const { client, calls } = setup([csrf(), json(200, {}), csrf()])
    const error = await client.post('/api/vehicles', {}).catch((e: unknown) => e)
    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).problem?.code).toBe('CSRF_INVALID')
    expect(calls).toHaveLength(3)
  })

  it('throws ApiError with the ProblemDetails for other errors', async () => {
    const { client } = setup([
      json(409, { status: 409, code: 'VEHICLE_LIMIT_REACHED', activeCount: 47, trashCount: 3 }, 'application/problem+json'),
    ])
    const error = (await client.post('/api/vehicles', {}).catch((e: unknown) => e)) as ApiError
    expect(error.status).toBe(409)
    expect(error.problem).toMatchObject({ code: 'VEHICLE_LIMIT_REACHED', trashCount: 3 })
  })

  it('reads a cookie value by name', () => {
    expect(readCookieFrom('a=1; XSRF-TOKEN=abc%3D; b=2', 'XSRF-TOKEN')).toBe('abc=')
    expect(readCookieFrom('a=1', 'XSRF-TOKEN')).toBeUndefined()
  })
})
