import { render, screen, waitFor } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'

// ── Mock del servicio ─────────────────────────────────────────────────────────
const mockMonthlyChart = vi.fn()
const mockSummary      = vi.fn()

vi.mock('@/services/transactionService', () => ({
  transactionService: {
    list:         vi.fn(),
    summary:      (...args: unknown[]) => mockSummary(...args),
    monthlyChart: (...args: unknown[]) => mockMonthlyChart(...args),
  },
}))

import { useMonthlyChart } from '@/hooks/useMonthlyChart'
import { useSummary }      from '@/hooks/useSummary'

// ── Wrapper ───────────────────────────────────────────────────────────────────
function makeWrapper() {
  const qc = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  return function W({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  }
}

// ── useMonthlyChart ───────────────────────────────────────────────────────────

function ChartHarness({ year }: { year: number }) {
  const { data, isLoading, isError } = useMonthlyChart(year)
  if (isLoading) return <span data-testid="loading" />
  if (isError)   return <span data-testid="error" />
  return <span data-testid="points">{data?.length ?? 0}</span>
}

describe('useMonthlyChart', () => {

  beforeEach(() => vi.resetAllMocks())

  it('devuelve loading mientras se resuelve', () => {
    mockMonthlyChart.mockReturnValue(new Promise(() => {}))
    render(<ChartHarness year={2026} />, { wrapper: makeWrapper() })
    expect(screen.getByTestId('loading')).toBeTruthy()
  })

  it('devuelve los puntos cuando el servicio resuelve', async () => {
    mockMonthlyChart.mockResolvedValue([
      { year: 2026, month: 1, income: 1000, expense: 600 },
      { year: 2026, month: 2, income: 800,  expense: 400 },
    ])
    render(<ChartHarness year={2026} />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('points').textContent).toBe('2'))
  })

  it('devuelve isError cuando el servicio falla', async () => {
    mockMonthlyChart.mockRejectedValue(new Error('server error'))
    render(<ChartHarness year={2026} />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('error')).toBeTruthy())
  })

  it('pasa el año correcto al servicio', async () => {
    mockMonthlyChart.mockResolvedValue([])
    render(<ChartHarness year={2025} />, { wrapper: makeWrapper() })
    await waitFor(() => expect(mockMonthlyChart).toHaveBeenCalledWith(2025))
  })
})

// ── useSummary ────────────────────────────────────────────────────────────────

function SummaryHarness({ from, to }: { from: string; to: string }) {
  const { data, isLoading, isError } = useSummary(from, to)
  if (isLoading) return <span data-testid="loading" />
  if (isError)   return <span data-testid="error" />
  return <span data-testid="balance">{data?.balance}</span>
}

describe('useSummary', () => {

  beforeEach(() => vi.resetAllMocks())

  it('devuelve loading mientras se resuelve', () => {
    mockSummary.mockReturnValue(new Promise(() => {}))
    render(<SummaryHarness from="2026-06-01" to="2026-06-30" />, { wrapper: makeWrapper() })
    expect(screen.getByTestId('loading')).toBeTruthy()
  })

  it('devuelve el balance cuando resuelve', async () => {
    mockSummary.mockResolvedValue({
      totalIncome: 2000, totalExpense: 1200, balance: 800, baseCurrency: 'EUR',
    })
    render(<SummaryHarness from="2026-06-01" to="2026-06-30" />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('balance').textContent).toBe('800'))
  })

  it('no hace fetch cuando from o to están vacíos', () => {
    render(<SummaryHarness from="" to="" />, { wrapper: makeWrapper() })
    expect(mockSummary).not.toHaveBeenCalled()
  })

  it('pasa from y to al servicio', async () => {
    mockSummary.mockResolvedValue({ totalIncome: 0, totalExpense: 0, balance: 0, baseCurrency: 'EUR' })
    render(<SummaryHarness from="2026-01-01" to="2026-01-31" />, { wrapper: makeWrapper() })
    await waitFor(() => expect(mockSummary).toHaveBeenCalledWith('2026-01-01', '2026-01-31'))
  })
})
