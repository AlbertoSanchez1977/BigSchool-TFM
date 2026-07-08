import { render, screen, waitFor, renderHook } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'

const mockListValuations   = vi.fn()
const mockCreateValuation  = vi.fn()
const mockValuationSeries  = vi.fn()

vi.mock('@/services/companyService', () => ({
  companyService: {
    listValuations:  (...args: unknown[]) => mockListValuations(...args),
    createValuation: (...args: unknown[]) => mockCreateValuation(...args),
    valuationSeries: (...args: unknown[]) => mockValuationSeries(...args),
  },
}))

import { useValuations, useCreateValuation, useValuationSeries } from '@/hooks/useValuations'

function makeWrapper(qc?: QueryClient) {
  const client = qc ?? new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>
  }
}

function makeValuation(overrides = {}) {
  return {
    idValuation: 1, idCompany: 5, price: 190.5, priceCurrency: 'USD',
    date: '2026-07-01', source: null,
    ...overrides,
  }
}

describe('useValuations', () => {
  beforeEach(() => vi.resetAllMocks())

  function ListHarness({ id }: { id: number }) {
    const { data, isLoading, isError } = useValuations(id, 1, 20)
    if (isLoading) return <span data-testid="loading" />
    if (isError) return <span data-testid="error" />
    return <span data-testid="count">{data?.items.length}</span>
  }

  it('llama a companyService.listValuations con id, page y pageSize', async () => {
    mockListValuations.mockResolvedValue({ items: [makeValuation()], meta: { page: 1, pageSize: 20, totalCount: 1, totalPages: 1 } })
    render(<ListHarness id={5} />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('count').textContent).toBe('1'))
    expect(mockListValuations).toHaveBeenCalledWith(5, 1, 20)
  })

  it('devuelve isError=true cuando el service lanza un error', async () => {
    mockListValuations.mockRejectedValue(new Error('network'))
    render(<ListHarness id={5} />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('error')).toBeTruthy())
  })
})

describe('useCreateValuation', () => {
  beforeEach(() => vi.resetAllMocks())

  it('llama a companyService.createValuation(id, dto)', async () => {
    mockCreateValuation.mockResolvedValue(makeValuation())
    const { result } = renderHook(() => useCreateValuation(5), { wrapper: makeWrapper() })

    result.current.mutate({ price: 190.5, date: '2026-07-01' })

    await waitFor(() => expect(mockCreateValuation).toHaveBeenCalledWith(5, { price: 190.5, date: '2026-07-01' }))
  })

  it('al éxito invalida el listado (["valuations", id]) y la serie (["valuation-series", id])', async () => {
    mockCreateValuation.mockResolvedValue(makeValuation())
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries')
    const { result } = renderHook(() => useCreateValuation(5), { wrapper: makeWrapper(qc) })

    result.current.mutate({ price: 190.5, date: '2026-07-01' })

    await waitFor(() => expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['valuations', 5] }))
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['valuation-series', 5] })
  })
})

describe('useValuationSeries', () => {
  beforeEach(() => vi.resetAllMocks())

  function SeriesHarness({ id, period }: { id: number; period: 'ThreeMonths' | 'OneYear' }) {
    const { data, isLoading } = useValuationSeries(id, period)
    if (isLoading) return <span data-testid="loading" />
    return <span data-testid="last">{data?.summary.last}</span>
  }

  it('llama a companyService.valuationSeries(id, period)', async () => {
    mockValuationSeries.mockResolvedValue({
      currency: 'USD',
      points: [{ date: '2026-07-01', price: 190.5 }],
      summary: { first: 180, last: 190.5, min: 175, max: 195, changePct: 5.83 },
    })
    render(<SeriesHarness id={5} period="OneYear" />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('last').textContent).toBe('190.5'))
    expect(mockValuationSeries).toHaveBeenCalledWith(5, 'OneYear')
  })
})
