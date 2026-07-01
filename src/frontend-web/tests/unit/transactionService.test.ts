import { describe, it, expect, vi, beforeEach } from 'vitest'
import { ApiError } from '@/lib/apiClient'

// ── Mock del singleton api ────────────────────────────────────────────────────
// Mockeamos @/lib/api para no hacer llamadas HTTP reales.
// Análogo a mockear IHttpClientFactory en un test unitario de C#.
const mockGet = vi.fn()
const mockGetWithMeta = vi.fn()

vi.mock('@/lib/api', () => ({
  api: {
    get: (...args: unknown[]) => mockGet(...args),
    getWithMeta: (...args: unknown[]) => mockGetWithMeta(...args),
  },
}))

import { transactionService } from '@/services/transactionService'

// ── Helpers ───────────────────────────────────────────────────────────────────

const defaultMeta = { page: 1, pageSize: 20, totalCount: 2, totalPages: 1 }

function makeRawItem(overrides = {}) {
  return {
    idTransaction: 1,
    type: 0,            // Income numérico (TransactionType.Income = 0)
    idMainCategory: 10, // Salary numérico (MainCategory.Salary = 10)
    idSubCategory: null,
    description: 'Nómina junio',
    transactionDate: '2026-06-30',
    originalAmount: 2850,
    originalCurrency: 'EUR',
    exchangeRate: 1,
    baseAmount: 2850,
    baseCurrency: 'EUR',
    rateDate: '2026-06-30',
    ...overrides,
  }
}

// ── transactionService.list ───────────────────────────────────────────────────

describe('transactionService.list', () => {

  beforeEach(() => {
    vi.resetAllMocks()
  })

  it('llama a /transactions con los filtros de fecha correctos', async () => {
    mockGetWithMeta.mockResolvedValue({ data: [], meta: defaultMeta })

    await transactionService.list({ from: '2026-06-01', to: '2026-06-30' })

    const [path] = mockGetWithMeta.mock.calls[0] as [string]
    expect(path).toContain('from=2026-06-01')
    expect(path).toContain('to=2026-06-30')
  })

  it('incluye filtro type cuando se pasa type', async () => {
    mockGetWithMeta.mockResolvedValue({ data: [], meta: defaultMeta })

    await transactionService.list({ type: 'Income' })

    const [path] = mockGetWithMeta.mock.calls[0] as [string]
    expect(path).toContain('type=Income')
  })

  it('incluye filtro category cuando se pasa idMainCategory', async () => {
    mockGetWithMeta.mockResolvedValue({ data: [], meta: defaultMeta })

    await transactionService.list({ idMainCategory: 'Salary' })

    const [path] = mockGetWithMeta.mock.calls[0] as [string]
    expect(path).toContain('category=Salary')
  })

  it('incluye page y pageSize en el query string', async () => {
    mockGetWithMeta.mockResolvedValue({ data: [], meta: defaultMeta })

    await transactionService.list({ page: 2, pageSize: 10 })

    const [path] = mockGetWithMeta.mock.calls[0] as [string]
    expect(path).toContain('page=2')
    expect(path).toContain('pageSize=10')
  })

  it('mapea type numérico 0 → "Income"', async () => {
    mockGetWithMeta.mockResolvedValue({
      data: [makeRawItem({ type: 0 })],
      meta: defaultMeta,
    })

    const result = await transactionService.list({})

    expect(result.items[0].type).toBe('Income')
  })

  it('mapea type numérico 1 → "Expense"', async () => {
    mockGetWithMeta.mockResolvedValue({
      data: [makeRawItem({ type: 1 })],
      meta: defaultMeta,
    })

    const result = await transactionService.list({})

    expect(result.items[0].type).toBe('Expense')
  })

  it('mapea idMainCategory numérico 10 → "Salary"', async () => {
    mockGetWithMeta.mockResolvedValue({
      data: [makeRawItem({ idMainCategory: 10 })],
      meta: defaultMeta,
    })

    const result = await transactionService.list({})

    expect(result.items[0].idMainCategory).toBe('Salary')
  })

  it('mapea idMainCategory numérico 1 → "EssentialExpenses"', async () => {
    mockGetWithMeta.mockResolvedValue({
      data: [makeRawItem({ idMainCategory: 1, type: 1 })],
      meta: defaultMeta,
    })

    const result = await transactionService.list({})

    expect(result.items[0].idMainCategory).toBe('EssentialExpenses')
  })

  it('devuelve el meta de paginación correctamente', async () => {
    const meta = { page: 2, pageSize: 10, totalCount: 35, totalPages: 4 }
    mockGetWithMeta.mockResolvedValue({ data: [], meta })

    const result = await transactionService.list({ page: 2 })

    expect(result.meta).toEqual(meta)
  })

  it('propaga ApiError cuando la api falla', async () => {
    mockGetWithMeta.mockRejectedValue(
      new ApiError('UNAUTHORIZED', 'Sesión expirada', undefined, 401)
    )

    await expect(transactionService.list({})).rejects.toBeInstanceOf(ApiError)
  })

  it('devuelve items vacío cuando data es null', async () => {
    mockGetWithMeta.mockResolvedValue({ data: null, meta: defaultMeta })

    const result = await transactionService.list({})

    expect(result.items).toEqual([])
  })
})

// ── transactionService.summary ────────────────────────────────────────────────

describe('transactionService.summary', () => {

  beforeEach(() => {
    vi.resetAllMocks()
  })

  it('llama a /transactions/summary con from y to', async () => {
    mockGet.mockResolvedValue({
      totalIncome: 2850, totalExpense: 1178, balance: 1672, baseCurrency: 'EUR',
    })

    await transactionService.summary('2026-06-01', '2026-06-30')

    const [path] = mockGet.mock.calls[0] as [string]
    expect(path).toContain('/transactions/summary')
    expect(path).toContain('from=2026-06-01')
    expect(path).toContain('to=2026-06-30')
  })

  it('devuelve el resumen correctamente', async () => {
    const summaryData = { totalIncome: 2850, totalExpense: 1178, balance: 1672, baseCurrency: 'EUR' }
    mockGet.mockResolvedValue(summaryData)

    const result = await transactionService.summary('2026-06-01', '2026-06-30')

    expect(result).toEqual(summaryData)
  })
})

// ── transactionService.monthlyChart ──────────────────────────────────────────

describe('transactionService.monthlyChart', () => {

  beforeEach(() => vi.resetAllMocks())

  it('llama a /transactions/monthly-chart?year=N', async () => {
    mockGet.mockResolvedValue([])

    await transactionService.monthlyChart(2026)

    const [path] = mockGet.mock.calls[0] as [string]
    expect(path).toContain('/transactions/monthly-chart')
    expect(path).toContain('year=2026')
  })

  it('devuelve el array de puntos mensuales', async () => {
    const points = [
      { year: 2026, month: 1, income: 1000, expense: 600 },
      { year: 2026, month: 2, income: 800,  expense: 400 },
    ]
    mockGet.mockResolvedValue(points)

    const result = await transactionService.monthlyChart(2026)

    expect(result).toHaveLength(2)
    expect(result[0]).toEqual({ year: 2026, month: 1, income: 1000, expense: 600 })
  })

  it('devuelve array vacío cuando no hay transacciones ese año', async () => {
    mockGet.mockResolvedValue([])

    const result = await transactionService.monthlyChart(2020)

    expect(result).toEqual([])
  })

  it('propaga ApiError cuando la api falla', async () => {
    mockGet.mockRejectedValue(
      new ApiError('UNAUTHORIZED', 'Sesión expirada', undefined, 401)
    )

    await expect(transactionService.monthlyChart(2026)).rejects.toBeInstanceOf(ApiError)
  })
})
