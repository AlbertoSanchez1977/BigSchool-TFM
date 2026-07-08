import { render, screen, fireEvent } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}))

const mockUsePortfolios = vi.fn()
const mockCreatePortfolio = vi.fn()
vi.mock('@/hooks/usePortfolios', () => ({
  usePortfolios: (...args: unknown[]) => mockUsePortfolios(...args),
  useCreatePortfolio: () => ({ mutate: mockCreatePortfolio, isPending: false }),
}))

// Las mutaciones de renombrar/borrar viven dentro de los modales compartidos
// (RenamePortfolioModal/DeletePortfolioModal), que solo se montan al abrir el modal.
vi.mock('@/hooks/usePortfolioMutations', () => ({
  useRenamePortfolio: () => ({ mutate: vi.fn(), isPending: false }),
  useDeletePortfolio: () => ({ mutate: vi.fn(), isPending: false }),
}))

import InvestmentsPage from '@/app/(private)/investments/page'

function makePortfolio(overrides = {}) {
  return {
    idPortfolio: 1, name: 'Mi cartera', realizedPnL: 0, realizedPnLCurrency: 'EUR',
    marketValue: 10000, costBasis: 9500, unrealizedPnL: 500, totalPnL: 500,
    ...overrides,
  }
}

describe('InvestmentsPage — card de cartera', () => {
  beforeEach(() => {
    mockPush.mockReset()
    mockUsePortfolios.mockReturnValue({
      data: { items: [makePortfolio()], meta: { page: 1, pageSize: 20, totalCount: 1, totalPages: 1 } },
      isLoading: false,
      isError: false,
    })
  })

  it('click en la card navega al detalle de la cartera', () => {
    render(<InvestmentsPage />)
    fireEvent.click(screen.getByTestId('portfolio-card'))
    expect(mockPush).toHaveBeenCalledWith('/investments/1')
  })

  it('click en "Renombrar" abre el modal y NO navega', () => {
    render(<InvestmentsPage />)
    fireEvent.click(screen.getByTestId('btn-rename-portfolio'))
    expect(mockPush).not.toHaveBeenCalled()
    expect(screen.getByText('Renombrar cartera')).toBeTruthy()
  })

  it('click en "Eliminar" abre el modal de confirmación y NO navega', () => {
    render(<InvestmentsPage />)
    fireEvent.click(screen.getByTestId('btn-delete-portfolio'))
    expect(mockPush).not.toHaveBeenCalled()
    expect(screen.getByText('Eliminar cartera')).toBeTruthy()
  })
})
