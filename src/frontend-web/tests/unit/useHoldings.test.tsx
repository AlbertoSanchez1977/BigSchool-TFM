import { render, screen, waitFor } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'

// ── Mocks ─────────────────────────────────────────────────────────────────────
const mockGetPortfolio = vi.fn()
const mockAdd          = vi.fn()
const mockUpdateNotes  = vi.fn()
const mockDelete       = vi.fn()

vi.mock('@/services/holdingsService', () => ({
  holdingsService: {
    getPortfolio: (...args: unknown[]) => mockGetPortfolio(...args),
    addHolding:   (...args: unknown[]) => mockAdd(...args),
    updateNotes:  (...args: unknown[]) => mockUpdateNotes(...args),
    deleteHolding:(...args: unknown[]) => mockDelete(...args),
  },
}))

import {
  usePortfolioDetail,
  useAddHolding,
  useUpdateHoldingNotes,
  useDeleteHolding,
} from '@/hooks/useHoldings'

// ── Wrapper ───────────────────────────────────────────────────────────────────
function makeWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return function W({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  }
}

// ── usePortfolioDetail ────────────────────────────────────────────────────────

function DetailHarness({ id }: { id: number }) {
  const { data, isLoading, isError } = usePortfolioDetail(id)
  if (isLoading) return <span data-testid="loading" />
  if (isError)   return <span data-testid="error" />
  return <span data-testid="name">{data?.name}</span>
}

describe('usePortfolioDetail', () => {

  beforeEach(() => vi.resetAllMocks())

  it('devuelve loading mientras se resuelve', () => {
    mockGetPortfolio.mockReturnValue(new Promise(() => {}))
    render(<DetailHarness id={1} />, { wrapper: makeWrapper() })
    expect(screen.getByTestId('loading')).toBeTruthy()
  })

  it('devuelve el nombre de la cartera cuando resuelve', async () => {
    mockGetPortfolio.mockResolvedValue({
      idPortfolio: 1, name: 'Mi cartera', realizedPnL: 0, realizedPnLCurrency: 'EUR', holdings: [],
    })
    render(<DetailHarness id={1} />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('name').textContent).toBe('Mi cartera'))
  })

  it('devuelve isError cuando falla', async () => {
    mockGetPortfolio.mockRejectedValue(new Error('not found'))
    render(<DetailHarness id={999} />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('error')).toBeTruthy())
  })

  it('pasa el id correcto al servicio', async () => {
    mockGetPortfolio.mockResolvedValue({
      idPortfolio: 7, name: 'Tech', realizedPnL: 0, realizedPnLCurrency: 'EUR', holdings: [],
    })
    render(<DetailHarness id={7} />, { wrapper: makeWrapper() })
    await waitFor(() => expect(mockGetPortfolio).toHaveBeenCalledWith(7))
  })
})

// ── useAddHolding ─────────────────────────────────────────────────────────────

function AddHarness({ portfolioId }: { portfolioId: number }) {
  const m = useAddHolding(portfolioId)
  return (
    <>
      <button type="button" onClick={() =>
        m.mutate({ idCompany: 10, shares: 5, buyPrice: 100, buyDate: '2024-01-01' })
      }>add</button>
      {m.isError && <span data-testid="add-error" />}
    </>
  )
}

describe('useAddHolding', () => {

  beforeEach(() => vi.resetAllMocks())

  it('llama al service con portfolioId y el DTO', async () => {
    mockAdd.mockResolvedValue({ idHolding: 1, idCompany: 10, shares: 5 })
    render(<AddHarness portfolioId={3} />, { wrapper: makeWrapper() })
    screen.getByText('add').click()
    await waitFor(() =>
      expect(mockAdd).toHaveBeenCalledWith(3, { idCompany: 10, shares: 5, buyPrice: 100, buyDate: '2024-01-01' })
    )
  })

  it('expone isError cuando el service falla', async () => {
    mockAdd.mockRejectedValue(new Error('fail'))
    render(<AddHarness portfolioId={1} />, { wrapper: makeWrapper() })
    screen.getByText('add').click()
    await waitFor(() => expect(screen.getByTestId('add-error')).toBeTruthy())
  })
})

// ── useUpdateHoldingNotes ─────────────────────────────────────────────────────

describe('useUpdateHoldingNotes', () => {

  beforeEach(() => vi.resetAllMocks())

  it('llama al service con portfolioId, holdingId y el DTO de notas', async () => {
    mockUpdateNotes.mockResolvedValue({ idHolding: 5, notes: 'long-term' })

    function NotesHarness() {
      const m = useUpdateHoldingNotes(1)
      return (
        <button type="button" onClick={() => m.mutate({ holdingId: 5, notes: 'long-term' })}>
          save
        </button>
      )
    }
    render(<NotesHarness />, { wrapper: makeWrapper() })
    screen.getByText('save').click()
    await waitFor(() =>
      expect(mockUpdateNotes).toHaveBeenCalledWith(1, 5, { notes: 'long-term' })
    )
  })
})

// ── useDeleteHolding ──────────────────────────────────────────────────────────

describe('useDeleteHolding', () => {

  beforeEach(() => vi.resetAllMocks())

  it('llama al service con portfolioId y holdingId', async () => {
    mockDelete.mockResolvedValue(undefined)

    function DelHarness() {
      const m = useDeleteHolding(2)
      return <button type="button" onClick={() => m.mutate(9)}>del</button>
    }
    render(<DelHarness />, { wrapper: makeWrapper() })
    screen.getByText('del').click()
    await waitFor(() => expect(mockDelete).toHaveBeenCalledWith(2, 9))
  })
})
