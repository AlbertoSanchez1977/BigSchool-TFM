import { render, screen, waitFor, renderHook } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'

const mockListPaged = vi.fn()
const mockGetById = vi.fn()
const mockCreate = vi.fn()

vi.mock('@/services/companyService', () => ({
  companyService: {
    listPaged: (...args: unknown[]) => mockListPaged(...args),
    getById:   (...args: unknown[]) => mockGetById(...args),
    create:    (...args: unknown[]) => mockCreate(...args),
  },
}))

import { useCompaniesList } from '@/hooks/useCompaniesList'
import { useCompanyDetail } from '@/hooks/useCompanyDetail'
import { useCreateCompany } from '@/hooks/useCreateCompany'

function makeWrapper(qc?: QueryClient) {
  const client = qc ?? new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>
  }
}

function makeCompany(overrides = {}) {
  return {
    idCompany: 1, name: 'Apple Inc.', ticker: 'AAPL', sector: 'Technology', market: 'NASDAQ',
    currency: 'USD', lastPrice: 190.5, lastValuationDate: '2026-07-01',
    ...overrides,
  }
}

describe('useCompaniesList', () => {
  beforeEach(() => vi.resetAllMocks())

  function ListHarness() {
    const { data, isLoading, isError } = useCompaniesList({ page: 1, pageSize: 20 })
    if (isLoading) return <span data-testid="loading" />
    if (isError) return <span data-testid="error" />
    return <span data-testid="count">{data?.items.length}</span>
  }

  it('llama a companyService.listPaged con page y pageSize', async () => {
    mockListPaged.mockResolvedValue({ items: [makeCompany()], meta: { page: 1, pageSize: 20, totalCount: 1, totalPages: 1 } })
    render(<ListHarness />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('count').textContent).toBe('1'))
    expect(mockListPaged).toHaveBeenCalledWith({ page: 1, pageSize: 20 })
  })

  it('devuelve isError=true cuando el service lanza un error', async () => {
    mockListPaged.mockRejectedValue(new Error('network'))
    render(<ListHarness />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('error')).toBeTruthy())
  })
})

describe('useCompanyDetail', () => {
  beforeEach(() => vi.resetAllMocks())

  function DetailHarness({ id }: { id: number }) {
    const { data, isLoading } = useCompanyDetail(id)
    if (isLoading) return <span data-testid="loading" />
    return <span data-testid="name">{data?.name}</span>
  }

  it('llama a companyService.getById(id)', async () => {
    mockGetById.mockResolvedValue(makeCompany({ name: 'Microsoft Corp.' }))
    render(<DetailHarness id={5} />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('name').textContent).toBe('Microsoft Corp.'))
    expect(mockGetById).toHaveBeenCalledWith(5)
  })
})

describe('useCreateCompany', () => {
  beforeEach(() => vi.resetAllMocks())

  it('llama a companyService.create con el DTO', async () => {
    mockCreate.mockResolvedValue({ idCompany: 9, name: 'Nueva', ticker: 'NEW', sector: null, market: null, currency: 'EUR' })
    const { result } = renderHook(() => useCreateCompany(), { wrapper: makeWrapper() })

    result.current.mutate({ name: 'Nueva', ticker: 'NEW', currency: 'EUR' })

    await waitFor(() => expect(mockCreate).toHaveBeenCalledWith({ name: 'Nueva', ticker: 'NEW', currency: 'EUR' }))
  })

  it('al éxito invalida la lista de empresas (["companies"])', async () => {
    mockCreate.mockResolvedValue({ idCompany: 9, name: 'Nueva', ticker: 'NEW', sector: null, market: null, currency: 'EUR' })
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries')
    const { result } = renderHook(() => useCreateCompany(), { wrapper: makeWrapper(qc) })

    result.current.mutate({ name: 'Nueva', ticker: 'NEW', currency: 'EUR' })

    await waitFor(() => expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['companies'] }))
  })
})
