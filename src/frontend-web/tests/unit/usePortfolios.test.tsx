import { render, screen, waitFor } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'

// ── Mock del servicio ─────────────────────────────────────────────────────────
const mockList = vi.fn()
const mockCreate = vi.fn()

vi.mock('@/services/portfolioService', () => ({
  portfolioService: {
    list:   (...args: unknown[]) => mockList(...args),
    create: (...args: unknown[]) => mockCreate(...args),
  },
}))

import { usePortfolios, useCreatePortfolio } from '@/hooks/usePortfolios'

// ── Wrapper con QueryClient fresco por test ───────────────────────────────────
function makeWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  }
}

// ── Harness para usePortfolios ────────────────────────────────────────────────
function PortfoliosHarness() {
  const { data, isLoading, isError } = usePortfolios()
  if (isLoading) return <span data-testid="loading" />
  if (isError)   return <span data-testid="error" />
  return (
    <ul>
      {(data ?? []).map((p) => (
        <li key={p.idPortfolio} data-testid="portfolio-row">{p.name}</li>
      ))}
    </ul>
  )
}

// ── Tests de usePortfolios ────────────────────────────────────────────────────

describe('usePortfolios', () => {

  beforeEach(() => vi.resetAllMocks())

  it('muestra estado de carga mientras se resuelve', () => {
    mockList.mockReturnValue(new Promise(() => {}))

    render(<PortfoliosHarness />, { wrapper: makeWrapper() })

    expect(screen.getByTestId('loading')).toBeTruthy()
  })

  it('devuelve la lista de carteras cuando el service resuelve', async () => {
    mockList.mockResolvedValue([
      { idPortfolio: 1, name: 'Mi cartera', realizedPnL: 0, realizedPnLCurrency: 'EUR',
        marketValue: 10000, costBasis: 9500, unrealizedPnL: 500, totalPnL: 500 },
    ])

    render(<PortfoliosHarness />, { wrapper: makeWrapper() })

    await waitFor(() => expect(screen.getByTestId('portfolio-row')).toBeTruthy())
    expect(screen.getByText('Mi cartera')).toBeTruthy()
  })

  it('devuelve isError cuando el service lanza error', async () => {
    mockList.mockRejectedValue(new Error('Network error'))

    render(<PortfoliosHarness />, { wrapper: makeWrapper() })

    await waitFor(() => expect(screen.getByTestId('error')).toBeTruthy())
  })
})

// ── Harness para useCreatePortfolio ──────────────────────────────────────────
function CreateHarness({ onSuccess }: { onSuccess: (id: number) => void }) {
  const mutation = useCreatePortfolio()
  return (
    <button
      type="button"
      data-testid="btn-create"
      onClick={() =>
        mutation.mutate({ name: 'Nueva' }, { onSuccess: (p) => onSuccess(p.idPortfolio) })
      }
    >
      Crear
    </button>
  )
}

// ── Tests de useCreatePortfolio ───────────────────────────────────────────────

describe('useCreatePortfolio', () => {

  beforeEach(() => vi.resetAllMocks())

  it('llama a portfolioService.create con el DTO correcto', async () => {
    mockCreate.mockResolvedValue({ idPortfolio: 1, name: 'Nueva', realizedPnL: 0, realizedPnLCurrency: 'EUR' })
    const onSuccess = vi.fn()

    render(<CreateHarness onSuccess={onSuccess} />, { wrapper: makeWrapper() })
    screen.getByTestId('btn-create').click()

    await waitFor(() => expect(mockCreate).toHaveBeenCalledWith({ name: 'Nueva' }))
  })

  it('llama a onSuccess con el idPortfolio del portfolio creado', async () => {
    mockCreate.mockResolvedValue({ idPortfolio: 42, name: 'Nueva', realizedPnL: 0, realizedPnLCurrency: 'EUR' })
    const onSuccess = vi.fn()

    render(<CreateHarness onSuccess={onSuccess} />, { wrapper: makeWrapper() })
    screen.getByTestId('btn-create').click()

    await waitFor(() => expect(onSuccess).toHaveBeenCalledWith(42))
  })

  it('expone isError cuando el service falla', async () => {
    mockCreate.mockRejectedValue(new Error('fail'))

    const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
    function ErrorHarness() {
      const m = useCreatePortfolio()
      return (
        <>
          <button type="button" onClick={() => m.mutate({ name: 'x' })}>go</button>
          {m.isError && <span data-testid="mutation-error" />}
        </>
      )
    }

    render(
      <QueryClientProvider client={qc}><ErrorHarness /></QueryClientProvider>
    )
    screen.getByText('go').click()

    await waitFor(() => expect(screen.getByTestId('mutation-error')).toBeTruthy())
  })
})
