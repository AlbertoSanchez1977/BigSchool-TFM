import { render, screen, waitFor } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'

const mockSummary = vi.fn()

vi.mock('@/services/portfolioService', () => ({
  portfolioService: {
    summary: (...args: unknown[]) => mockSummary(...args),
  },
}))

import { usePortfoliosSummary } from '@/hooks/usePortfoliosSummary'

function makeWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  }
}

function SummaryHarness() {
  const { data, isLoading, isError } = usePortfoliosSummary()
  if (isLoading) return <span data-testid="loading" />
  if (isError) return <span data-testid="error" />
  return <span data-testid="market-value">{data?.marketValue}</span>
}

describe('usePortfoliosSummary', () => {
  beforeEach(() => vi.resetAllMocks())

  it('devuelve estado de carga antes de resolver', () => {
    mockSummary.mockReturnValue(new Promise(() => {}))
    render(<SummaryHarness />, { wrapper: makeWrapper() })
    expect(screen.getByTestId('loading')).toBeTruthy()
  })

  it('llama a portfolioService.summary() y devuelve el resultado', async () => {
    mockSummary.mockResolvedValue({
      baseCurrency: 'EUR', marketValue: 12000, costBasis: 10000, unrealizedPnL: 2000,
      realizedPnL: 500, totalPnL: 2500, returnPct: 25, portfolioCount: 3,
    })
    render(<SummaryHarness />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('market-value').textContent).toBe('12000'))
    expect(mockSummary).toHaveBeenCalled()
  })

  it('devuelve isError=true cuando el service lanza un error', async () => {
    mockSummary.mockRejectedValue(new Error('network'))
    render(<SummaryHarness />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('error')).toBeTruthy())
  })
})
