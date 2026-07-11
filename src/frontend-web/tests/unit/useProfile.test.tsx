import { render, screen, waitFor, renderHook } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'

const mockMe = vi.fn()
const mockUpdate = vi.fn()

vi.mock('@/services/userService', () => ({
  userService: {
    me: (...args: unknown[]) => mockMe(...args),
    update: (...args: unknown[]) => mockUpdate(...args),
  },
}))

import { useProfile, useUpdateProfile } from '@/hooks/useProfile'

function makeWrapper() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  }
}

function ProfileHarness() {
  const { data, isLoading, isError } = useProfile()
  if (isLoading) return <span data-testid="loading" />
  if (isError) return <span data-testid="error" />
  return <span data-testid="fullName">{data?.fullName}</span>
}

describe('useProfile', () => {
  beforeEach(() => vi.resetAllMocks())

  it('devuelve estado de carga antes de resolver', () => {
    mockMe.mockReturnValue(new Promise(() => {}))
    render(<ProfileHarness />, { wrapper: makeWrapper() })
    expect(screen.getByTestId('loading')).toBeTruthy()
  })

  it('devuelve el perfil del backend cuando resuelve', async () => {
    mockMe.mockResolvedValue({
      idUser: 1, email: 'ana@x.com', fullName: 'Ana', baseCurrency: 'EUR', lastLoginDate: '2026-07-01T09:00:00',
    })
    render(<ProfileHarness />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('fullName').textContent).toBe('Ana'))
    expect(mockMe).toHaveBeenCalled()
  })

  it('devuelve isError=true cuando el service lanza un error', async () => {
    mockMe.mockRejectedValue(new Error('network'))
    render(<ProfileHarness />, { wrapper: makeWrapper() })
    await waitFor(() => expect(screen.getByTestId('error')).toBeTruthy())
  })
})

describe('useUpdateProfile', () => {
  beforeEach(() => vi.resetAllMocks())

  it('llama a userService.update con fullName y password', async () => {
    mockUpdate.mockResolvedValue({
      idUser: 1, email: 'ana@x.com', fullName: 'Ana Nueva', baseCurrency: 'EUR', lastLoginDate: null,
    })
    const { result } = renderHook(() => useUpdateProfile(), { wrapper: makeWrapper() })

    result.current.mutate({ fullName: 'Ana Nueva', password: null })

    await waitFor(() => expect(mockUpdate).toHaveBeenCalledWith({ fullName: 'Ana Nueva', password: null }))
  })

  it('tras actualizar, useProfile refleja los datos nuevos sin refetch (setQueryData)', async () => {
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    function Wrapper({ children }: { children: ReactNode }) {
      return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
    }

    mockMe.mockResolvedValue({
      idUser: 1, email: 'ana@x.com', fullName: 'Ana', baseCurrency: 'EUR', lastLoginDate: null,
    })
    mockUpdate.mockResolvedValue({
      idUser: 1, email: 'ana@x.com', fullName: 'Ana Nueva', baseCurrency: 'EUR', lastLoginDate: null,
    })

    render(<ProfileHarness />, { wrapper: Wrapper })
    await waitFor(() => expect(screen.getByTestId('fullName').textContent).toBe('Ana'))

    const { result } = renderHook(() => useUpdateProfile(), { wrapper: Wrapper })
    result.current.mutate({ fullName: 'Ana Nueva', password: null })

    await waitFor(() => expect(screen.getByTestId('fullName').textContent).toBe('Ana Nueva'))
  })
})
