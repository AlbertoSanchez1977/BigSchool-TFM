import { render, screen, waitFor, renderHook } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'

// Mockeamos el service (no fetch/api directamente) para aislar el hook,
// igual que en useTransactions.test.tsx. Equivale a mockear un repositorio
// inyectado en un servicio de aplicación C#.
const mockListContacts = vi.fn()
const mockCreateContact = vi.fn()

vi.mock('@/services/notificationService', () => ({
  notificationService: {
    listContacts: (...args: unknown[]) => mockListContacts(...args),
    createContact: (...args: unknown[]) => mockCreateContact(...args),
  },
}))

import { useContacts, useCreateContact } from '@/hooks/useContacts'

function makeWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  }
}

function ContactsHarness() {
  const { contacts, isLoading, isError } = useContacts()
  if (isLoading) return <span data-testid="loading" />
  if (isError) return <span data-testid="error" />
  return (
    <ul>
      {contacts.map((c) => (
        <li key={c.idContact} data-testid="row">{c.fullName}</li>
      ))}
    </ul>
  )
}

describe('useContacts', () => {
  beforeEach(() => vi.resetAllMocks())

  it('devuelve estado de carga antes de resolver', () => {
    mockListContacts.mockReturnValue(new Promise(() => {}))
    render(<ContactsHarness />, { wrapper: makeWrapper() })
    expect(screen.getByTestId('loading')).toBeTruthy()
  })

  it('devuelve los contactos del backend cuando resuelve', async () => {
    mockListContacts.mockResolvedValue([
      { idContact: 1, fullName: 'Ana', email: 'a@x.com', message: 'hola', createdAt: '2026-07-06T10:00:00' },
    ])
    render(<ContactsHarness />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('row')).toBeTruthy())
    expect(screen.getByText('Ana')).toBeTruthy()
    expect(mockListContacts).toHaveBeenCalled()
  })

  it('devuelve isError=true cuando el service lanza un error', async () => {
    mockListContacts.mockRejectedValue(new Error('network'))
    render(<ContactsHarness />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('error')).toBeTruthy())
  })
})

describe('useCreateContact', () => {
  beforeEach(() => vi.resetAllMocks())

  it('llama a notificationService.createContact con los datos del formulario', async () => {
    mockCreateContact.mockResolvedValue({ idContact: 1 })
    const { result } = renderHook(() => useCreateContact(), { wrapper: makeWrapper() })

    result.current.mutate({ fullName: 'Ana', email: 'a@x.com', message: 'hola' })

    await waitFor(() => expect(mockCreateContact).toHaveBeenCalledWith({
      fullName: 'Ana', email: 'a@x.com', message: 'hola',
    }))
  })
})
