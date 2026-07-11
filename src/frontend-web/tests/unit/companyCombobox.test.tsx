import { render, screen, fireEvent } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'

const mockUseCompanies = vi.fn()
vi.mock('@/hooks/useCompanies', () => ({
  useCompanies: (...args: unknown[]) => mockUseCompanies(...args),
}))

import { CompanyCombobox } from '@/components/market/company-combobox'

function makeCompany(overrides = {}) {
  return {
    idCompany: 1, name: 'Apple Inc.', ticker: 'AAPL', sector: 'Technology', market: 'NASDAQ',
    currency: 'USD', lastPrice: 190.5, lastValuationDate: '2026-07-01',
    ...overrides,
  }
}

describe('CompanyCombobox', () => {
  beforeEach(() => vi.resetAllMocks())

  it('muestra el placeholder cuando no hay empresa seleccionada', () => {
    mockUseCompanies.mockReturnValue({ data: [makeCompany()] })
    render(<CompanyCombobox value={null} onChange={vi.fn()} />)
    expect(screen.getByText('Selecciona empresa…')).toBeTruthy()
  })

  it('al abrir, escribir filtra la lista por ticker/nombre', () => {
    mockUseCompanies.mockReturnValue({
      data: [
        makeCompany({ idCompany: 1, ticker: 'AAPL', name: 'Apple Inc.' }),
        makeCompany({ idCompany: 2, ticker: 'MSFT', name: 'Microsoft Corp.' }),
      ],
    })
    render(<CompanyCombobox value={null} onChange={vi.fn()} />)

    fireEvent.click(screen.getByRole('combobox'))
    const input = screen.getByPlaceholderText('Selecciona empresa…')
    fireEvent.change(input, { target: { value: 'MSFT' } })

    expect(screen.getByText(/MSFT/)).toBeTruthy()
    expect(screen.queryByText(/AAPL/)).toBeNull()
  })

  it('al elegir una empresa llama a onChange con el idCompany', () => {
    mockUseCompanies.mockReturnValue({ data: [makeCompany({ idCompany: 7, ticker: 'AAPL', name: 'Apple Inc.' })] })
    const onChange = vi.fn()
    render(<CompanyCombobox value={null} onChange={onChange} />)

    fireEvent.click(screen.getByRole('combobox'))
    fireEvent.click(screen.getByText(/AAPL/))

    expect(onChange).toHaveBeenCalledWith(7)
  })
})
