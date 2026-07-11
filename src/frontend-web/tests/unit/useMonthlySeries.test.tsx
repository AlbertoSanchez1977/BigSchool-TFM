import { renderHook, waitFor } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'

const mockMonthly = vi.fn()

vi.mock('@/services/transactionService', () => ({
  transactionService: {
    monthly: (...args: unknown[]) => mockMonthly(...args),
  },
}))

import { useMonthlySeries } from '@/hooks/useMonthlySeries'

function makeWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  }
}

describe('useMonthlySeries', () => {
  beforeEach(() => vi.resetAllMocks())

  it('pide monthly una vez por cada categoría del tipo (7 para Expense)', async () => {
    mockMonthly.mockResolvedValue([])
    renderHook(() => useMonthlySeries('Expense', 2026, { enabled: true }), { wrapper: makeWrapper() })

    await waitFor(() => expect(mockMonthly).toHaveBeenCalledTimes(7))
    expect(mockMonthly).toHaveBeenCalledWith('2023-01-01', '2026-12-31', 'Expense', 'EssentialExpenses')
    expect(mockMonthly).toHaveBeenCalledWith('2023-01-01', '2026-12-31', 'Expense', 'Amortizations')
  })

  it('pide monthly una vez por cada categoría del tipo (4 para Income)', async () => {
    mockMonthly.mockResolvedValue([])
    renderHook(() => useMonthlySeries('Income', 2026, { enabled: true }), { wrapper: makeWrapper() })

    await waitFor(() => expect(mockMonthly).toHaveBeenCalledTimes(4))
    expect(mockMonthly).toHaveBeenCalledWith('2023-01-01', '2026-12-31', 'Income', 'Salary')
  })

  it('devuelve una entrada { category, points } por cada categoría del tipo', async () => {
    mockMonthly.mockImplementation((_f: string, _t: string, _ty: string, category: string) => {
      if (category === 'Salary') {
        return Promise.resolve([{ year: 2026, month: 6, income: 2850, expense: 0 }])
      }
      return Promise.resolve([])
    })

    const { result } = renderHook(() => useMonthlySeries('Income', 2026, { enabled: true }), { wrapper: makeWrapper() })

    expect(result.current.data).toHaveLength(4)
    await waitFor(() => {
      const salary = result.current.data.find((d) => d.category === 'Salary')
      expect(salary?.points).toEqual([{ year: 2026, month: 6, income: 2850, expense: 0 }])
    })
  })

  it('no dispara llamadas cuando enabled=false', () => {
    mockMonthly.mockResolvedValue([])
    renderHook(() => useMonthlySeries('Expense', 2026, { enabled: false }), { wrapper: makeWrapper() })
    expect(mockMonthly).not.toHaveBeenCalled()
  })
})
