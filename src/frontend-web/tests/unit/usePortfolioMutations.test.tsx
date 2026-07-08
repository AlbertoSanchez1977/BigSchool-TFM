import { renderHook, waitFor } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'

const mockRename = vi.fn()
const mockRemove = vi.fn()

vi.mock('@/services/portfolioService', () => ({
  portfolioService: {
    rename: (...args: unknown[]) => mockRename(...args),
    remove: (...args: unknown[]) => mockRemove(...args),
  },
}))

import { useRenamePortfolio, useDeletePortfolio } from '@/hooks/usePortfolioMutations'

function makeWrapper(qc: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  }
}

describe('useRenamePortfolio', () => {
  beforeEach(() => vi.resetAllMocks())

  it('llama a portfolioService.rename(id, data)', async () => {
    mockRename.mockResolvedValue(undefined)
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const { result } = renderHook(() => useRenamePortfolio(7), { wrapper: makeWrapper(qc) })

    result.current.mutate({ name: 'Cartera renombrada' })

    await waitFor(() => expect(mockRename).toHaveBeenCalledWith(7, { name: 'Cartera renombrada' }))
  })

  it('al éxito invalida el detalle (["portfolios", id]) y la lista (["portfolios"])', async () => {
    mockRename.mockResolvedValue(undefined)
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries')
    const { result } = renderHook(() => useRenamePortfolio(7), { wrapper: makeWrapper(qc) })

    result.current.mutate({ name: 'X' })

    await waitFor(() => expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['portfolios', 7] }))
    expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['portfolios'] })
  })
})

describe('useDeletePortfolio', () => {
  beforeEach(() => vi.resetAllMocks())

  it('llama a portfolioService.remove(id)', async () => {
    mockRemove.mockResolvedValue(undefined)
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const { result } = renderHook(() => useDeletePortfolio(), { wrapper: makeWrapper(qc) })

    result.current.mutate(7)

    await waitFor(() => expect(mockRemove).toHaveBeenCalledWith(7))
  })

  it('al éxito invalida la lista de carteras (["portfolios"])', async () => {
    mockRemove.mockResolvedValue(undefined)
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const invalidateSpy = vi.spyOn(qc, 'invalidateQueries')
    const { result } = renderHook(() => useDeletePortfolio(), { wrapper: makeWrapper(qc) })

    result.current.mutate(7)

    await waitFor(() => expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ['portfolios'] }))
  })

  it('propaga el error (409 guard fiscal) vía isError', async () => {
    mockRemove.mockRejectedValue(new Error('Hay holdings abiertos'))
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
    const { result } = renderHook(() => useDeletePortfolio(), { wrapper: makeWrapper(qc) })

    result.current.mutate(7)

    await waitFor(() => expect(result.current.isError).toBe(true))
  })
})
