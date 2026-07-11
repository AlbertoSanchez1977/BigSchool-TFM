import { render, screen, waitFor } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'

const mockListEmails = vi.fn()

vi.mock('@/services/notificationService', () => ({
  notificationService: {
    listEmails: (...args: unknown[]) => mockListEmails(...args),
  },
}))

import { useEmails } from '@/hooks/useEmails'

function makeWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  }
}

function EmailsHarness() {
  const { emails, isLoading, isError } = useEmails()
  if (isLoading) return <span data-testid="loading" />
  if (isError) return <span data-testid="error" />
  return (
    <ul>
      {emails.map((e) => (
        <li key={e.idEmailLog} data-testid="row">{e.subject}</li>
      ))}
    </ul>
  )
}

describe('useEmails', () => {
  beforeEach(() => vi.resetAllMocks())

  it('devuelve estado de carga antes de resolver', () => {
    mockListEmails.mockReturnValue(new Promise(() => {}))
    render(<EmailsHarness />, { wrapper: makeWrapper() })
    expect(screen.getByTestId('loading')).toBeTruthy()
  })

  it('devuelve los emails del backend cuando resuelve (bienvenida + contacto)', async () => {
    mockListEmails.mockResolvedValue([
      { idEmailLog: 1, idUser: 7, recipient: 'ana@x.com', subject: 'Bienvenido a BigSchool', type: 1, sentAt: '2026-07-01T09:00:00' },
      { idEmailLog: 2, idUser: null, recipient: 'soporte@bigschool.com', subject: 'Nuevo mensaje de contacto', type: 2, sentAt: '2026-07-02T09:00:00' },
    ])
    render(<EmailsHarness />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getAllByTestId('row')).toHaveLength(2))
    expect(mockListEmails).toHaveBeenCalled()
  })

  it('devuelve isError=true cuando el service lanza un error', async () => {
    mockListEmails.mockRejectedValue(new Error('network'))
    render(<EmailsHarness />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('error')).toBeTruthy())
  })
})
