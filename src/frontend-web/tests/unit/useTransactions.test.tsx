import { render, screen, waitFor } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'

// ── Mock del service ──────────────────────────────────────────────────────────
// Mockeamos el service para aislar el hook: probamos el comportamiento
// (estados loading/success/error) sin depender de la red.
// En C# sería inyectar un ITransactionRepository mockeado en el servicio de aplicación.
const mockList = vi.fn()

vi.mock('@/services/transactionService', () => ({
  transactionService: {
    list: (...args: unknown[]) => mockList(...args),
  },
}))

import { useTransactions } from '@/hooks/useTransactions'

// ── Wrapper con QueryClient fresco por test ───────────────────────────────────
// TanStack Query necesita su QueryClientProvider para funcionar.
// Creamos uno con caché desactivada para que cada test sea independiente.
function makeWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  }
}

// Componente de prueba que expone el estado del hook en el DOM
function HookHarness({ filters = {} }: { filters?: object }) {
  const { data, isLoading, isError } = useTransactions(filters as never)
  if (isLoading) return <span data-testid="loading" />
  if (isError)   return <span data-testid="error" />
  return (
    <ul>
      {(data?.items ?? []).map((t) => (
        <li key={t.idTransaction} data-testid="row">{t.description}</li>
      ))}
    </ul>
  )
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('useTransactions', () => {

  beforeEach(() => {
    vi.resetAllMocks()
  })

  it('devuelve estado de carga (isLoading=true) antes de resolver', () => {
    // La promesa nunca resuelve → el hook queda en loading
    mockList.mockReturnValue(new Promise(() => {}))

    render(<HookHarness />, { wrapper: makeWrapper() })

    expect(screen.getByTestId('loading')).toBeTruthy()
  })

  it('devuelve los datos cuando el service resuelve correctamente', async () => {
    mockList.mockResolvedValue({
      items: [
        {
          idTransaction: 1, type: 'Income', idMainCategory: 'Salary',
          idSubCategory: null, description: 'Nómina junio',
          transactionDate: '2026-06-30', originalAmount: 2850,
          originalCurrency: 'EUR', exchangeRate: 1, baseAmount: 2850,
          baseCurrency: 'EUR', rateDate: '2026-06-30',
        },
      ],
      meta: { page: 1, pageSize: 20, totalCount: 1, totalPages: 1 },
    })

    render(<HookHarness />, { wrapper: makeWrapper() })

    await waitFor(() => expect(screen.getByTestId('row')).toBeTruthy())
    expect(screen.getByText('Nómina junio')).toBeTruthy()
  })

  it('devuelve isError=true cuando el service lanza un error', async () => {
    mockList.mockRejectedValue(new Error('Network error'))

    render(<HookHarness />, { wrapper: makeWrapper() })

    await waitFor(() => expect(screen.getByTestId('error')).toBeTruthy())
  })

  it('incluye los filtros en la queryKey para re-fetch al cambiar filtros', () => {
    // Verificamos que las queries con filtros distintos tienen queryKeys distintas.
    // Si tuvieran la misma key, cambiar el mes no dispararía un nuevo fetch.
    // Aquí solo verificamos que el hook llama al service con los filtros correctos.
    mockList.mockResolvedValue({ items: [], meta: { page: 1, pageSize: 20, totalCount: 0, totalPages: 0 } })

    const filters = { from: '2026-05-01', to: '2026-05-31' }
    render(<HookHarness filters={filters} />, { wrapper: makeWrapper() })

    // El service debe recibir los filtros exactos
    return waitFor(() => {
      expect(mockList).toHaveBeenCalledWith(filters)
    })
  })
})
