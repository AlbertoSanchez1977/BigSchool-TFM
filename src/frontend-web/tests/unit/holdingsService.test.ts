import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ApiError } from '@/lib/apiClient'

// ── Mocks ─────────────────────────────────────────────────────────────────────
const mockGet  = vi.fn()
const mockPost = vi.fn()
const mockPut  = vi.fn()
const mockDel  = vi.fn()

vi.mock('@/lib/api', () => ({
  api: {
    get:    (...args: unknown[]) => mockGet(...args),
    post:   (...args: unknown[]) => mockPost(...args),
    put:    (...args: unknown[]) => mockPut(...args),
    delete: (...args: unknown[]) => mockDel(...args),
  },
}))

import { holdingsService } from '@/services/holdingsService'

// ── Helpers ───────────────────────────────────────────────────────────────────

function makeDetail(overrides = {}) {
  return {
    idPortfolio: 1, name: 'Mi cartera',
    realizedPnL: 0, realizedPnLCurrency: 'EUR',
    holdings: [],
    ...overrides,
  }
}

function makeHoldingListItem(overrides = {}) {
  return {
    idHolding: 1, idCompany: 10, ticker: 'AAPL', companyCurrency: 'USD',
    shares: 10, openShares: 10,
    buyOriginalAmount: 150, buyOriginalCurrency: 'USD', buyExchangeRate: 1.08,
    buyBaseAmount: 138.88, buyBaseCurrency: 'EUR', buyRateDate: '2024-01-15',
    buyDate: '2024-01-15', notes: null,
    marketValue: 1800, costBasis: 1388.8, unrealizedPnL: 411.2,
    ...overrides,
  }
}

function makeHoldingDto(overrides = {}) {
  return {
    idHolding: 1, idCompany: 10,
    shares: 10,
    buyOriginalAmount: 150, buyOriginalCurrency: 'USD', buyExchangeRate: 1.08,
    buyBaseAmount: 138.88, buyBaseCurrency: 'EUR', buyRateDate: '2024-01-15',
    buyDate: '2024-01-15', notes: null,
    ...overrides,
  }
}

// ── holdingsService.getPortfolio ──────────────────────────────────────────────

describe('holdingsService.getPortfolio', () => {

  beforeEach(() => vi.resetAllMocks())

  it('llama a GET /portfolios/{id}', async () => {
    mockGet.mockResolvedValue(makeDetail())

    await holdingsService.getPortfolio(7)

    expect(mockGet).toHaveBeenCalledWith('/portfolios/7')
  })

  it('devuelve el detalle de la cartera con sus holdings', async () => {
    const detail = makeDetail({ holdings: [makeHoldingListItem()] })
    mockGet.mockResolvedValue(detail)

    const result = await holdingsService.getPortfolio(1)

    expect(result.name).toBe('Mi cartera')
    expect(result.holdings).toHaveLength(1)
    expect(result.holdings[0].ticker).toBe('AAPL')
  })

  it('propaga ApiError en caso de error', async () => {
    mockGet.mockRejectedValue(new ApiError('ENTITY_NOT_FOUND', 'Cartera no encontrada', undefined, 404))

    await expect(holdingsService.getPortfolio(999)).rejects.toBeInstanceOf(ApiError)
  })
})

// ── holdingsService.addHolding ────────────────────────────────────────────────

describe('holdingsService.addHolding', () => {

  beforeEach(() => vi.resetAllMocks())

  it('llama a POST /portfolios/{id}/holdings con el DTO correcto', async () => {
    mockPost.mockResolvedValue(makeHoldingDto())
    const dto = { idCompany: 10, shares: 10, buyPrice: 150, buyDate: '2024-01-15' }

    await holdingsService.addHolding(1, dto)

    expect(mockPost).toHaveBeenCalledWith('/portfolios/1/holdings', dto)
  })

  it('devuelve el HoldingDto creado', async () => {
    const dto = makeHoldingDto({ idHolding: 42 })
    mockPost.mockResolvedValue(dto)

    const result = await holdingsService.addHolding(1, { idCompany: 10, shares: 10, buyPrice: 150, buyDate: '2024-01-15' })

    expect(result.idHolding).toBe(42)
  })

  it('propaga ApiError en validación fallida', async () => {
    mockPost.mockRejectedValue(new ApiError('VALIDATION', 'Shares obligatorio', 'shares', 400))

    await expect(holdingsService.addHolding(1, { idCompany: 10, shares: 0, buyPrice: 150, buyDate: '2024-01-15' }))
      .rejects.toBeInstanceOf(ApiError)
  })
})

// ── holdingsService.updateNotes ───────────────────────────────────────────────

describe('holdingsService.updateNotes', () => {

  beforeEach(() => vi.resetAllMocks())

  it('llama a PUT /portfolios/{id}/holdings/{holdingId}', async () => {
    mockPut.mockResolvedValue(makeHoldingDto({ notes: 'Compra long-term' }))

    await holdingsService.updateNotes(1, 5, { notes: 'Compra long-term' })

    expect(mockPut).toHaveBeenCalledWith('/portfolios/1/holdings/5', { notes: 'Compra long-term' })
  })

  it('devuelve el holding actualizado', async () => {
    mockPut.mockResolvedValue(makeHoldingDto({ notes: 'Actualizado' }))

    const result = await holdingsService.updateNotes(1, 5, { notes: 'Actualizado' })

    expect(result.notes).toBe('Actualizado')
  })
})

// ── holdingsService.deleteHolding ─────────────────────────────────────────────

describe('holdingsService.deleteHolding', () => {

  beforeEach(() => vi.resetAllMocks())

  it('llama a DELETE /portfolios/{id}/holdings/{holdingId}', async () => {
    mockDel.mockResolvedValue(undefined)

    await holdingsService.deleteHolding(1, 5)

    expect(mockDel).toHaveBeenCalledWith('/portfolios/1/holdings/5')
  })
})
