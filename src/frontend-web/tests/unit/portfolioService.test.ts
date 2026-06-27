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

// ── portfolioService.performance ──────────────────────────────────────────────

function makePerformance(overrides = {}) {
  return {
    idPortfolio: 1,
    name: 'Mi cartera',
    baseCurrency: 'EUR',
    marketValue: 12000,
    costBasis: 10000,
    unrealizedPnL: 2000,
    realizedPnL: 500,
    totalPnL: 2500,
    returnPct: 25,
    holdings: [],
    ...overrides,
  }
}

describe('portfolioService.performance', () => {

  beforeEach(() => vi.resetAllMocks())

  it('llama a GET /portfolios/{id}/performance', async () => {
    mockGet.mockResolvedValue(makePerformance())

    await portfolioService.performance(5)

    expect(mockGet).toHaveBeenCalledWith('/portfolios/5/performance')
  })

  it('devuelve PortfolioPerformance con returnPct correcto', async () => {
    const perf = makePerformance({ returnPct: 15.5 })
    mockGet.mockResolvedValue(perf)

    const result = await portfolioService.performance(1)

    expect(result.returnPct).toBe(15.5)
    expect(result.baseCurrency).toBe('EUR')
    expect(result.holdings).toEqual([])
  })

  it('propaga ApiError cuando la cartera no existe', async () => {
    mockGet.mockRejectedValue(
      new ApiError('ENTITY_NOT_FOUND', 'Cartera no encontrada', undefined, 404)
    )

    await expect(portfolioService.performance(999)).rejects.toBeInstanceOf(ApiError)
  })
})

// ── portfolioService.sellShares ───────────────────────────────────────────────

function makeSellResult(overrides = {}) {
  return {
    disposals: [],
    portfolioRealizedPnL: 250,
    realizedPnLCurrency: 'EUR',
    ...overrides,
  }
}

describe('portfolioService.sellShares', () => {

  beforeEach(() => vi.resetAllMocks())

  it('llama a POST /portfolios/{id}/sales con el DTO', async () => {
    mockPost.mockResolvedValue(makeSellResult())

    await portfolioService.sellShares(3, {
      companyId: 10, shares: 5, sellPrice: 150, sellDate: '2024-06-01',
    })

    expect(mockPost).toHaveBeenCalledWith('/portfolios/3/sales', {
      companyId: 10, shares: 5, sellPrice: 150, sellDate: '2024-06-01',
    })
  })

  it('devuelve SellSharesResult con el PnL actualizado', async () => {
    const sellResult = makeSellResult({ portfolioRealizedPnL: 750 })
    mockPost.mockResolvedValue(sellResult)

    const res = await portfolioService.sellShares(1, {
      companyId: 5, shares: 10, sellPrice: 200, sellDate: '2024-01-15',
    })

    expect(res.portfolioRealizedPnL).toBe(750)
    expect(res.disposals).toEqual([])
    expect(res.realizedPnLCurrency).toBe('EUR')
  })

  it('propaga ApiError cuando las acciones son insuficientes', async () => {
    mockPost.mockRejectedValue(
      new ApiError('INSUFFICIENT_SHARES', 'Acciones insuficientes', undefined, 400)
    )

    await expect(
      portfolioService.sellShares(1, { companyId: 5, shares: 999, sellPrice: 100, sellDate: '2024-01-01' })
    ).rejects.toBeInstanceOf(ApiError)
  })
})
