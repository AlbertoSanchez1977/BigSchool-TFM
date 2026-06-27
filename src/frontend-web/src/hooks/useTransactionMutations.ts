import { useMutation, useQueryClient } from '@tanstack/react-query'
import { transactionService } from '@/services/transactionService'
import type { CreateTransactionDto, UpdateTransactionDto } from '@/types/transactions'

// invalidateQueries con queryKey: ['transactions'] invalida CUALQUIER query cuya clave
// empiece por ese prefijo: tanto ['transactions', filters] (lista) como
// ['transactions', 'summary', ...] (KPIs). Equivale a un cache eviction en C#.

export function useCreateTransaction() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (data: CreateTransactionDto) => transactionService.create(data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['transactions'] }) },
  })
}

export function useUpdateTransaction() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, data }: { id: number; data: UpdateTransactionDto }) =>
      transactionService.update(id, data),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['transactions'] }) },
  })
}

export function useDeleteTransaction() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => transactionService.delete(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['transactions'] }) },
  })
}
