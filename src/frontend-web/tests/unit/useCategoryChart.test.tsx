import { renderHook, waitFor } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'

// La costura ahora tira del backend: el hook hace 4 llamadas a `by-category`
// (una por año de la ventana) en vez de agregar en cliente sobre `useTransactions`.
const mockByCategory = vi.fn()

vi.mock('@/services/transactionService', () => ({
  transactionService: {
    byCategory: (...args: unknown[]) => mockByCategory(...args),
  },
}))

import { useCategoryChart } from '@/hooks/useCategoryChart'

function makeWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  }
}

describe('useCategoryChart', () => {
  beforeEach(() => vi.resetAllMocks())

  it('pide by-category una vez por cada uno de los 4 años de la ventana', async () => {
    mockByCategory.mockResolvedValue([])
    renderHook(() => useCategoryChart('Expense', 2026, { enabled: true }), { wrapper: makeWrapper() })

    await waitFor(() => expect(mockByCategory).toHaveBeenCalledTimes(4))
    expect(mockByCategory).toHaveBeenCalledWith('2023-01-01', '2023-12-31', 'Expense')
    expect(mockByCategory).toHaveBeenCalledWith('2026-01-01', '2026-12-31', 'Expense')
  })

  it('mapea los 4 CategoryTotal[] al mismo output CategoryAggregation { years, rows }', async () => {
    mockByCategory.mockImplementation((from: string) => {
      if (from === '2026-01-01') {
        return Promise.resolve([{ idMainCategory: 1, mainCategory: 'EssentialExpenses', total: 100 }])
      }
      return Promise.resolve([])
    })

    const { result } = renderHook(() => useCategoryChart('Expense', 2026, { enabled: true }), { wrapper: makeWrapper() })

    await waitFor(() => expect(result.current.data.rows).toHaveLength(1))
    expect(result.current.data.years).toEqual([2023, 2024, 2025, 2026])
    expect(result.current.data.rows[0].category).toBe('EssentialExpenses')
    expect(result.current.data.rows[0].totals).toEqual({ 2023: 0, 2024: 0, 2025: 0, 2026: 100 })
  })

  it('no dispara llamadas cuando enabled=false', () => {
    mockByCategory.mockResolvedValue([])
    renderHook(() => useCategoryChart('Expense', 2026, { enabled: false }), { wrapper: makeWrapper() })
    expect(mockByCategory).not.toHaveBeenCalled()
  })

  it('isError=true si alguna de las 4 llamadas falla', async () => {
    mockByCategory.mockResolvedValue([])
    mockByCategory.mockRejectedValueOnce(new Error('network'))

    const { result } = renderHook(() => useCategoryChart('Expense', 2026, { enabled: true }), { wrapper: makeWrapper() })

    await waitFor(() => expect(result.current.isError).toBe(true))
  })
})
