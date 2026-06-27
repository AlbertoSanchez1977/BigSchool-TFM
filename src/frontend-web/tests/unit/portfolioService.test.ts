import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ApiError } from '@/lib/apiClient'

// ── Mock del singleton api ────────────────────────────────────────────────────
const mockGet = vi.fn()
const mockPost = vi.fn()

vi.mock('@/lib/api', () => ({
  api: {
    get:  (...args: unknown[]) => mockGet(...args),
    post: (...args: unknown[]) => mockPost(...args),
  },
}))

import { portfolioService } from '@/services/portfolioService'

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeListItem(overrides = {}) {
  return {
    idPortfolio: 1,
    name: 'Mi cartera',
    realizedPnL: 0,
    realizedPnLCurrency: 'EUR',
    marketValue: 10000,
    costBasis: 9500,
    unrealizedPnL: 500,
    totalPnL: 500,
    ...overrides,
  }
}

// ── portfolioService.list ─────────────────────────────────────────────────────

describe('portfolioService.list', () => {

  beforeEach(() => vi.resetAllMocks())

  it('llama a GET /portfolios', async () => {
    mockGet.mockResolvedValue([])

    await portfolioService.list()

    expect(mockGet).toHaveBeenCalledWith('/portfolios')
  })

  it('devuelve la lista de carteras correctamente', async () => {
    const items = [makeListItem(), makeListItem({ idPortfolio: 2, name: 'USA tech' })]
    mockGet.mockResolvedValue(items)

    const result = await portfolioService.list()

    expect(result).toHaveLength(2)
    expect(result[0].name).toBe('Mi cartera')
    expect(result[1].name).toBe('USA tech')
  })

  it('devuelve lista vacía si el backend devuelve []', async () => {
    mockGet.mockResolvedValue([])

    const result = await portfolioService.list()

    expect(result).toEqual([])
  })

  it('propaga ApiError cuando la api falla', async () => {
    mockGet.mockRejectedValue(
      new ApiError('UNAUTHORIZED', 'Sesión expirada', undefined, 401)
    )

    await expect(portfolioService.list()).rejects.toBeInstanceOf(ApiError)
  })
})

// ── portfolioService.create ───────────────────────────────────────────────────

describe('portfolioService.create', () => {

  beforeEach(() => vi.resetAllMocks())

  it('llama a POST /portfolios con el nombre', async () => {
    mockPost.mockResolvedValue({ idPortfolio: 1, name: 'Mi cartera', realizedPnL: 0, realizedPnLCurrency: 'EUR' })

    await portfolioService.create({ name: 'Mi cartera' })

    expect(mockPost).toHaveBeenCalledWith('/portfolios', { name: 'Mi cartera' })
  })

  it('devuelve el Portfolio creado', async () => {
    const portfolio = { idPortfolio: 42, name: 'Global', realizedPnL: 0, realizedPnLCurrency: 'EUR' as const }
    mockPost.mockResolvedValue(portfolio)

    const result = await portfolioService.create({ name: 'Global' })

    expect(result).toEqual(portfolio)
  })

  it('propaga ApiError cuando la validación falla', async () => {
    mockPost.mockRejectedValue(
      new ApiError('VALIDATION', 'Nombre obligatorio', 'name', 400)
    )

    await expect(portfolioService.create({ name: '' })).rejects.toBeInstanceOf(ApiError)
  })
})
