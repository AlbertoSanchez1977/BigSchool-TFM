import { renderHook, waitFor } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import type { Transaction } from '@/types/transactions'

// ── Mock del service ──────────────────────────────────────────────────────────
// Aislamos los hooks del service real para no hacer llamadas HTTP.
// En C# sería inyectar un mock de ITransactionRepository.
const mockCreate = vi.fn()
const mockUpdate = vi.fn()
const mockDelete = vi.fn()

vi.mock('@/services/transactionService', () => ({
  transactionService: {
    list:    vi.fn(),
    summary: vi.fn(),
    create:  (...args: unknown[]) => mockCreate(...args),
    update:  (...args: unknown[]) => mockUpdate(...args),
    delete:  (...args: unknown[]) => mockDelete(...args),
  },
}))

import { useCreateTransaction, useUpdateTransaction, useDeleteTransaction } from '@/hooks/useTransactionMutations'

// ── QueryClient fresco por test ───────────────────────────────────────────────
// Igual que en useTransactions.test — cada test tiene su propio cliente
// para que la caché no contamine entre tests.
function makeWrapper() {
  const qc = new QueryClient({
    defaultOptions: {
      queries:   { retry: false },
      mutations: { retry: false },
    },
  })
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  }
}

// ── Datos de prueba ───────────────────────────────────────────────────────────

const validPayload = {
  type:            'Expense'           as const,
  idMainCategory:  'EssentialExpenses' as const,
  transactionDate: '2026-06-30',
  amount:          150,
  currency:        'EUR'               as const,
}

const mockTx: Transaction = {
  idTransaction:    1,
  type:             'Expense',
  idMainCategory:   'EssentialExpenses',
  idSubCategory:    null,
  description:      null,
  transactionDate:  '2026-06-30',
  originalAmount:   150,
  originalCurrency: 'EUR',
  exchangeRate:     1,
  baseAmount:       150,
  baseCurrency:     'EUR',
  rateDate:         '2026-06-30',
}

// ── useCreateTransaction ──────────────────────────────────────────────────────

describe('useCreateTransaction', () => {

  beforeEach(() => vi.resetAllMocks())

  it('llama a transactionService.create con los datos del formulario', async () => {
    mockCreate.mockResolvedValue(mockTx)

    const { result } = renderHook(() => useCreateTransaction(), { wrapper: makeWrapper() })

    result.current.mutate(validPayload)

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(mockCreate).toHaveBeenCalledWith(validPayload)
  })

  it('expone isError cuando el service lanza un error', async () => {
    mockCreate.mockRejectedValue(new Error('Error de red'))

    const { result } = renderHook(() => useCreateTransaction(), { wrapper: makeWrapper() })

    result.current.mutate(validPayload)

    await waitFor(() => expect(result.current.isError).toBe(true))
  })

})

// ── useUpdateTransaction ──────────────────────────────────────────────────────

describe('useUpdateTransaction', () => {

  beforeEach(() => vi.resetAllMocks())

  it('llama a transactionService.update con el id y los datos', async () => {
    mockUpdate.mockResolvedValue(mockTx)

    const { result } = renderHook(() => useUpdateTransaction(), { wrapper: makeWrapper() })

    result.current.mutate({ id: 42, data: validPayload })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(mockUpdate).toHaveBeenCalledWith(42, validPayload)
  })

})

// ── useDeleteTransaction ──────────────────────────────────────────────────────

describe('useDeleteTransaction', () => {

  beforeEach(() => vi.resetAllMocks())

  it('llama a transactionService.delete con el id de la transacción', async () => {
    mockDelete.mockResolvedValue(undefined)

    const { result } = renderHook(() => useDeleteTransaction(), { wrapper: makeWrapper() })

    result.current.mutate(42)

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(mockDelete).toHaveBeenCalledWith(42)
  })

})
