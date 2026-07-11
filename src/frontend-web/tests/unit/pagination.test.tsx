import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi } from 'vitest'
import { Pagination } from '@/components/ui/pagination'

describe('Pagination', () => {
  it('muestra el rango "X–Y de Z"', () => {
    render(<Pagination page={2} pageSize={20} totalCount={45} totalPages={3} onPageChange={vi.fn()} />)
    expect(screen.getByText('21–40 de 45')).toBeTruthy()
  })

  it('el rango recorta el "hasta" en la última página', () => {
    render(<Pagination page={3} pageSize={20} totalCount={45} totalPages={3} onPageChange={vi.fn()} />)
    expect(screen.getByText('41–45 de 45')).toBeTruthy()
  })

  it('el botón Anterior está deshabilitado en la página 1', () => {
    render(<Pagination page={1} pageSize={20} totalCount={45} totalPages={3} onPageChange={vi.fn()} />)
    expect(screen.getByText('Anterior').closest('button')).toBeDisabled()
  })

  it('el botón Siguiente está deshabilitado en la última página', () => {
    render(<Pagination page={3} pageSize={20} totalCount={45} totalPages={3} onPageChange={vi.fn()} />)
    expect(screen.getByText('Siguiente').closest('button')).toBeDisabled()
  })

  it('click en Siguiente llama a onPageChange con page+1', () => {
    const onPageChange = vi.fn()
    render(<Pagination page={1} pageSize={20} totalCount={45} totalPages={3} onPageChange={onPageChange} />)
    fireEvent.click(screen.getByText('Siguiente'))
    expect(onPageChange).toHaveBeenCalledWith(2)
  })

  it('click en Anterior llama a onPageChange con page-1', () => {
    const onPageChange = vi.fn()
    render(<Pagination page={2} pageSize={20} totalCount={45} totalPages={3} onPageChange={onPageChange} />)
    fireEvent.click(screen.getByText('Anterior'))
    expect(onPageChange).toHaveBeenCalledWith(1)
  })

  it('no renderiza nada si totalPages <= 1', () => {
    const { container } = render(<Pagination page={1} pageSize={20} totalCount={5} totalPages={1} onPageChange={vi.fn()} />)
    expect(container).toBeEmptyDOMElement()
  })
})
