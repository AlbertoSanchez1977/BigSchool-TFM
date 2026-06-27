import { describe, it, expect } from 'vitest'
import { transactionSchema } from '@/lib/schemas/transaction'

// Caso base válido (gasto). Los tests que necesitan variaciones hacen spread: { ...valid, ... }
const valid = {
  type: 'Expense',
  idMainCategory: 'EssentialExpenses',
  transactionDate: '2026-06-30',
  amount: 150,
  currency: 'EUR',
}

describe('transactionSchema', () => {

  it('acepta un gasto válido con todos los campos obligatorios', () => {
    expect(transactionSchema.safeParse(valid).success).toBe(true)
  })

  it('acepta un ingreso válido', () => {
    const income = { ...valid, type: 'Income', idMainCategory: 'Salary' }
    expect(transactionSchema.safeParse(income).success).toBe(true)
  })

  it('falla cuando amount es cero', () => {
    const { success, error } = transactionSchema.safeParse({ ...valid, amount: 0 })
    expect(success).toBe(false)
    expect(error?.issues[0].message).toMatch(/positivo|mayor/i)
  })

  it('falla cuando amount es negativo', () => {
    const { success } = transactionSchema.safeParse({ ...valid, amount: -50 })
    expect(success).toBe(false)
  })

  it('falla con amount como string (la conversión la hace react-hook-form, no el schema)', () => {
    // El form usa { valueAsNumber: true } → llega al schema ya como number.
    // El schema recibe strings solo en tests directos — ahí debe fallar.
    const { success } = transactionSchema.safeParse({ ...valid, amount: '250' })
    expect(success).toBe(false)
  })

  it('falla con type inválido', () => {
    const { success } = transactionSchema.safeParse({ ...valid, type: 'Transfer' })
    expect(success).toBe(false)
  })

  it('falla con idMainCategory inválida', () => {
    const { success } = transactionSchema.safeParse({ ...valid, idMainCategory: 'Unknown' })
    expect(success).toBe(false)
  })

  it('falla con fecha en formato incorrecto', () => {
    const { success } = transactionSchema.safeParse({ ...valid, transactionDate: '30/06/2026' })
    expect(success).toBe(false)
  })

  it('falla con description mayor de 500 caracteres', () => {
    const { success } = transactionSchema.safeParse({ ...valid, description: 'x'.repeat(501) })
    expect(success).toBe(false)
  })

  it('acepta idSubCategory null (campo opcional)', () => {
    const { success } = transactionSchema.safeParse({ ...valid, idSubCategory: null })
    expect(success).toBe(true)
  })

})
