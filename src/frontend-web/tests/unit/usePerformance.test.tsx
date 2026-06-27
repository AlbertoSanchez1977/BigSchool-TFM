import { render, screen, waitFor } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'

// ── Mock del servicio ─────────────────────────────────────────────────────────
const mockPerformance = vi.fn()
const mockSell        = vi.fn()

vi.mock('@/services/portfolioService', () => ({
  portfolioService: {
    list:        vi.fn(),
    getById:     vi.fn(),
    create:      vi.fn(),
    performance: (...args: unknown[]) => mockPerformance(...args),
    sellShares:  (...args: unknown[]) => mockSell(...args),
  },
}))

import { usePerformance, useSellShares } from '@/hooks/usePerformance'

// ── Wrapper con QueryClient fresco por test ───────────────────────────────────
function makeWrapper() {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return function W({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  }
}

// ── usePerformance ────────────────────────────────────────────────────────────

function PerfHarness({ id, enabled }: { id: number; enabled?: boolean }) {
  const { data, isLoading, isError } = usePerformance(id, { enabled })
  if (isLoading) return <span data-testid="loading" />
  if (isError)   return <span data-testid="error" />
  return <span data-testid="pct">{data?.returnPct}</span>
}

describe('usePerformance', () => {

  beforeEach(() => vi.resetAllMocks())

  it('devuelve loading mientras se resuelve', () => {
    mockPerformance.mockReturnValue(new Promise(() => {}))
    render(<PerfHarness id={1} />, { wrapper: makeWrapper() })
    expect(screen.getByTestId('loading')).toBeTruthy()
  })

  it('devuelve los datos de performance cuando resuelve', async () => {
    mockPerformance.mockResolvedValue({
      idPortfolio: 1, name: 'Mi cartera', baseCurrency: 'EUR',
      marketValue: 12000, costBasis: 10000, unrealizedPnL: 2000,
      realizedPnL: 500, totalPnL: 2500, returnPct: 12.5, holdings: [],
    })
    render(<PerfHarness id={1} />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('pct').textContent).toBe('12.5'))
  })

  it('devuelve isError cuando el servicio falla', async () => {
    mockPerformance.mockRejectedValue(new Error('not found'))
    render(<PerfHarness id={99} />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('error')).toBeTruthy())
  })

  it('no hace fetch cuando enabled=false', () => {
    render(<PerfHarness id={1} enabled={false} />, { wrapper: makeWrapper() })
    expect(mockPerformance).not.toHaveBeenCalled()
  })

  it('pasa el id correcto al servicio', async () => {
    mockPerformance.mockResolvedValue({
      idPortfolio: 7, name: 'Global', baseCurrency: 'USD',
      marketValue: 1000, costBasis: 900, unrealizedPnL: 100,
      realizedPnL: 0, totalPnL: 100, returnPct: 11.1, holdings: [],
    })
    render(<PerfHarness id={7} />, { wrapper: makeWrapper() })
    await waitFor(() => expect(mockPerformance).toHaveBeenCalledWith(7))
  })
})

// ── useSellShares ─────────────────────────────────────────────────────────────

function SellHarness({ portfolioId }: { portfolioId: number }) {
  const m = useSellShares(portfolioId)
  return (
    <>
      <button
        type="button"
        onClick={() =>
          m.mutate({ companyId: 10, shares: 5, sellPrice: 150, sellDate: '2024-06-01' })
        }
      >
        sell
      </button>
      {m.isError && <span data-testid="sell-error" />}
    </>
  )
}

describe('useSellShares', () => {

  beforeEach(() => vi.resetAllMocks())

  it('llama al service con portfolioId y el DTO de venta', async () => {
    mockSell.mockResolvedValue({
      disposals: [], portfolioRealizedPnL: 250, realizedPnLCurrency: 'EUR',
    })
    render(<SellHarness portfolioId={3} />, { wrapper: makeWrapper() })
    screen.getByText('sell').click()
    await waitFor(() =>
      expect(mockSell).toHaveBeenCalledWith(3, {
        companyId: 10, shares: 5, sellPrice: 150, sellDate: '2024-06-01',
      })
    )
  })

  it('expone isError cuando el servicio falla', async () => {
    mockSell.mockRejectedValue(new Error('Acciones insuficientes'))
    render(<SellHarness portfolioId={1} />, { wrapper: makeWrapper() })
    screen.getByText('sell').click()
    await waitFor(() => expect(screen.getByTestId('sell-error')).toBeTruthy())
  })
})
