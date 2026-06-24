import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { createApiClient, ApiError } from '@/lib/apiClient'

// El módulo de token se conectará en la Task 2 (auth). Por ahora lo mockeamos con un
// getter inyectable — el mismo patrón que usaríamos con un ITokenProvider en C#.
const mockGetToken = vi.fn<() => string | null>()
const mockOnUnauthorized = vi.fn()

function makeClient() {
  return createApiClient({
    baseUrl: 'http://localhost:5285',
    getToken: mockGetToken,
    onUnauthorized: mockOnUnauthorized,
  })
}

beforeEach(() => {
  vi.resetAllMocks()
  mockGetToken.mockReturnValue(null)
})

afterEach(() => {
  vi.restoreAllMocks()
})

describe('apiClient', () => {
  it('devuelve data del envelope en una respuesta 200', async () => {
    const payload = { id: 1, name: 'Test' }
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: true,
        status: 200,
        json: async () => ({ data: payload, errors: [], meta: null }),
      }),
    )

    const client = makeClient()
    const result = await client.get('/test')

    expect(result).toEqual(payload)
  })

  it('lanza ApiError con code/message/field cuando errors[] no está vacío', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        status: 400,
        json: async () => ({
          data: null,
          errors: [{ code: 'INVALID_EMAIL', message: 'Email inválido', field: 'email' }],
          meta: null,
        }),
      }),
    )

    const client = makeClient()
    await expect(client.get('/test')).rejects.toMatchObject({
      code: 'INVALID_EMAIL',
      message: 'Email inválido',
      field: 'email',
    })
  })

  it('lanza ApiError genérico en HTTP no-2xx sin envelope válido', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        status: 500,
        json: async () => { throw new Error('not json') },
      }),
    )

    const client = makeClient()
    await expect(client.get('/test')).rejects.toBeInstanceOf(ApiError)
  })

  it('inyecta Authorization: Bearer cuando hay token', async () => {
    mockGetToken.mockReturnValue('mi-token-jwt')
    const fetchSpy = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ data: {}, errors: [], meta: null }),
    })
    vi.stubGlobal('fetch', fetchSpy)

    const client = makeClient()
    await client.get('/test')

    const [, options] = fetchSpy.mock.calls[0] as [string, RequestInit]
    expect((options.headers as Record<string, string>)['Authorization']).toBe('Bearer mi-token-jwt')
  })

  it('NO incluye Authorization cuando no hay token', async () => {
    mockGetToken.mockReturnValue(null)
    const fetchSpy = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: async () => ({ data: {}, errors: [], meta: null }),
    })
    vi.stubGlobal('fetch', fetchSpy)

    const client = makeClient()
    await client.get('/test')

    const [, options] = fetchSpy.mock.calls[0] as [string, RequestInit]
    expect((options.headers as Record<string, string>)['Authorization']).toBeUndefined()
  })

  it('invoca onUnauthorized cuando la respuesta es 401', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        status: 401,
        json: async () => ({ data: null, errors: [], meta: null }),
      }),
    )

    const client = makeClient()
    await expect(client.get('/test')).rejects.toBeInstanceOf(ApiError)
    expect(mockOnUnauthorized).toHaveBeenCalledOnce()
  })
})
