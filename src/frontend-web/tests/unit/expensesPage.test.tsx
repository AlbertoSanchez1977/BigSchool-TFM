import { render, screen } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'

// ── Mocks de navegación y auth ────────────────────────────────────────────────
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  usePathname: () => '/expenses',
}))

vi.mock('@/hooks/useAuth', () => ({
  useAuth: () => ({ user: { fullName: 'Test User', email: 'u@b.com' }, isLoading: false }),
}))

// ── Mock del hook de datos ────────────────────────────────────────────────────
// Controlamos el estado (loading/data/error) desde cada test.
const mockUseTransactions = vi.fn()
const mockUseSummary = vi.fn()

vi.mock('@/hooks/useTransactions', () => ({
  useTransactions: (...args: unknown[]) => mockUseTransactions(...args),
}))

vi.mock('@/hooks/useSummary', () => ({
  useSummary: (...args: unknown[]) => mockUseSummary(...args),
}))

// useCategories resuelve los nombres de subcategoría en la tabla. La página lo llama
// siempre, así que lo mockeamos para no necesitar QueryClientProvider en este test.
vi.mock('@/hooks/useCategories', () => ({
  useCategories: () => ({ data: [] }),
}))

// useCategoryChart/useMonthlySeries usan useQueries de TanStack Query directamente
// (no pasan por un hook ya mockeado como useTransactions), así que sin este mock
// harían falta un QueryClientProvider real. Esta suite no ejercita la pestaña de
// Gráficas, así que basta con un valor neutro.
vi.mock('@/hooks/useCategoryChart', () => ({
  useCategoryChart: () => ({ data: { years: [], rows: [] }, isLoading: false, isError: false }),
}))

vi.mock('@/hooks/useMonthlySeries', () => ({
  useMonthlySeries: () => ({ data: [], isLoading: false, isError: false }),
}))

import ExpensesPage from '@/app/(private)/expenses/page'

// ── Helpers ───────────────────────────────────────────────────────────────────

const emptySummary = { data: { totalIncome: 0, totalExpense: 0, balance: 0, baseCurrency: 'EUR' }, isLoading: false, isError: false }

function makeTx(id: number, description: string) {
  return {
    idTransaction: id, type: 'Income' as const, idMainCategory: 'Salary' as const,
    idSubCategory: null, description,
    transactionDate: '2026-06-30', originalAmount: 100,
    originalCurrency: 'EUR' as const, exchangeRate: 1, baseAmount: 100,
    baseCurrency: 'EUR' as const, rateDate: '2026-06-30',
  }
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('ExpensesPage', () => {

  beforeEach(() => {
    vi.resetAllMocks()
    mockUseSummary.mockReturnValue(emptySummary)
  })

  it('muestra skeleton mientras cargan las transacciones', () => {
    mockUseTransactions.mockReturnValue({ data: undefined, isLoading: true, isError: false })

    render(<ExpensesPage />)

    expect(screen.getAllByTestId('skeleton-row').length).toBeGreaterThan(0)
  })

  it('muestra mensaje vacío cuando no hay transacciones', () => {
    mockUseTransactions.mockReturnValue({
      data: { items: [], meta: { page: 1, pageSize: 20, totalCount: 0, totalPages: 0 } },
      isLoading: false,
      isError: false,
    })

    render(<ExpensesPage />)

    expect(screen.getByTestId('empty-state')).toBeTruthy()
  })

  it('muestra una fila por transacción cuando hay datos', () => {
    mockUseTransactions.mockReturnValue({
      data: {
        items: [makeTx(1, 'Nómina junio'), makeTx(2, 'Alquiler')],
        meta: { page: 1, pageSize: 20, totalCount: 2, totalPages: 1 },
      },
      isLoading: false,
      isError: false,
    })

    render(<ExpensesPage />)

    // tx-row está solo en la presentación desktop (las tarjetas móviles no lo llevan),
    // por eso el conteo es exactamente 2 aunque jsdom renderice ambas vistas.
    expect(screen.getAllByTestId('tx-row').length).toBe(2)
    // La descripción ya no se muestra como texto (es un icono con title); en su lugar
    // verificamos que la categoría (Nómina = Salary) aparece en las filas renderizadas.
    expect(screen.getAllByText('Nómina').length).toBeGreaterThan(0)
  })

  it('muestra mensaje de error cuando la carga falla', () => {
    mockUseTransactions.mockReturnValue({ data: undefined, isLoading: false, isError: true })

    render(<ExpensesPage />)

    expect(screen.getByTestId('error-state')).toBeTruthy()
  })

  it('muestra el selector de mes/año', () => {
    mockUseTransactions.mockReturnValue({
      data: { items: [], meta: { page: 1, pageSize: 20, totalCount: 0, totalPages: 0 } },
      isLoading: false,
      isError: false,
    })

    render(<ExpensesPage />)

    expect(screen.getByTestId('month-selector')).toBeTruthy()
  })
})
