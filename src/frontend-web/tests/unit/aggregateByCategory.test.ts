import { describe, it, expect } from 'vitest'
import { aggregateByCategory } from '@/lib/charts/aggregateByCategory'
import type { Transaction } from '@/types/transactions'
import type { MainCategory, TransactionType } from '@/types/enums'

// ── Helper: fabrica una Transaction con lo mínimo que la agregación usa ─────────
// La función solo mira: type, idMainCategory, transactionDate y baseAmount.
// El resto se rellena con valores válidos pero irrelevantes para el test.
function tx(
  partial: {
    type: TransactionType
    idMainCategory: MainCategory
    transactionDate: string
    baseAmount: number
    originalAmount?: number
  },
): Transaction {
  return {
    idTransaction: Math.floor(Math.random() * 1e6),
    type: partial.type,
    idMainCategory: partial.idMainCategory,
    idSubCategory: null,
    description: null,
    transactionDate: partial.transactionDate,
    originalAmount: partial.originalAmount ?? partial.baseAmount,
    originalCurrency: 'EUR',
    exchangeRate: 1,
    baseAmount: partial.baseAmount,
    baseCurrency: 'EUR',
    rateDate: partial.transactionDate,
  }
}

describe('aggregateByCategory', () => {

  it('devuelve la ventana de los últimos 4 años en orden ascendente', () => {
    const { years } = aggregateByCategory([], 'Expense', 2026)
    expect(years).toEqual([2023, 2024, 2025, 2026])
  })

  it('con lista vacía devuelve years pero sin filas', () => {
    const result = aggregateByCategory([], 'Expense', 2026)
    expect(result.rows).toEqual([])
    expect(result.years).toEqual([2023, 2024, 2025, 2026])
  })

  it('agrupa por categoría y año (varios años)', () => {
    const result = aggregateByCategory(
      [
        tx({ type: 'Expense', idMainCategory: 'EssentialExpenses', transactionDate: '2024-03-10', baseAmount: 100 }),
        tx({ type: 'Expense', idMainCategory: 'EssentialExpenses', transactionDate: '2025-07-01', baseAmount: 200 }),
        tx({ type: 'Expense', idMainCategory: 'EssentialExpenses', transactionDate: '2026-01-15', baseAmount: 50 }),
      ],
      'Expense',
      2026,
    )
    const row = result.rows.find((r) => r.category === 'EssentialExpenses')!
    expect(row.totals[2023]).toBe(0)
    expect(row.totals[2024]).toBe(100)
    expect(row.totals[2025]).toBe(200)
    expect(row.totals[2026]).toBe(50)
  })

  it('suma varias transacciones de la misma categoría y año', () => {
    const result = aggregateByCategory(
      [
        tx({ type: 'Expense', idMainCategory: 'Luxuries', transactionDate: '2026-02-01', baseAmount: 30 }),
        tx({ type: 'Expense', idMainCategory: 'Luxuries', transactionDate: '2026-02-20', baseAmount: 20 }),
        tx({ type: 'Expense', idMainCategory: 'Luxuries', transactionDate: '2026-11-05', baseAmount: 15 }),
      ],
      'Expense',
      2026,
    )
    const row = result.rows.find((r) => r.category === 'Luxuries')!
    expect(row.totals[2026]).toBe(65)
  })

  it('mantiene categorías mixtas como filas independientes', () => {
    const result = aggregateByCategory(
      [
        tx({ type: 'Expense', idMainCategory: 'EssentialExpenses', transactionDate: '2026-01-10', baseAmount: 100 }),
        tx({ type: 'Expense', idMainCategory: 'Education', transactionDate: '2026-01-12', baseAmount: 80 }),
        tx({ type: 'Expense', idMainCategory: 'Luxuries', transactionDate: '2026-01-14', baseAmount: 40 }),
      ],
      'Expense',
      2026,
    )
    expect(result.rows.map((r) => r.category).sort()).toEqual(
      ['Education', 'EssentialExpenses', 'Luxuries'],
    )
  })

  it('excluye transacciones fuera de la ventana de 4 años', () => {
    const result = aggregateByCategory(
      [
        tx({ type: 'Expense', idMainCategory: 'EssentialExpenses', transactionDate: '2022-12-31', baseAmount: 999 }),
        tx({ type: 'Expense', idMainCategory: 'EssentialExpenses', transactionDate: '2023-01-01', baseAmount: 10 }),
      ],
      'Expense',
      2026,
    )
    const row = result.rows.find((r) => r.category === 'EssentialExpenses')!
    // La de 2022 queda fuera; solo cuenta la de 2023.
    expect(row.totals[2023]).toBe(10)
    expect(Object.values(row.totals).reduce((a, b) => a + b, 0)).toBe(10)
  })

  it('separa ingreso de gasto: solo agrega el tipo pedido', () => {
    const data = [
      tx({ type: 'Expense', idMainCategory: 'EssentialExpenses', transactionDate: '2026-01-10', baseAmount: 100 }),
      tx({ type: 'Income', idMainCategory: 'Salary', transactionDate: '2026-01-10', baseAmount: 3000 }),
    ]

    const expense = aggregateByCategory(data, 'Expense', 2026)
    expect(expense.rows.map((r) => r.category)).toEqual(['EssentialExpenses'])

    const income = aggregateByCategory(data, 'Income', 2026)
    expect(income.rows.map((r) => r.category)).toEqual(['Salary'])
  })

  it('usa baseAmount (no originalAmount) para sumar', () => {
    const result = aggregateByCategory(
      [
        // originalAmount en otra divisa; baseAmount es el normalizado a la moneda base.
        tx({ type: 'Expense', idMainCategory: 'EssentialExpenses', transactionDate: '2026-03-01', baseAmount: 90, originalAmount: 100 }),
      ],
      'Expense',
      2026,
    )
    const row = result.rows.find((r) => r.category === 'EssentialExpenses')!
    expect(row.totals[2026]).toBe(90)
  })

  it('redondea a 2 decimales para evitar ruido de coma flotante', () => {
    const result = aggregateByCategory(
      [
        tx({ type: 'Expense', idMainCategory: 'EssentialExpenses', transactionDate: '2026-01-01', baseAmount: 0.1 }),
        tx({ type: 'Expense', idMainCategory: 'EssentialExpenses', transactionDate: '2026-01-02', baseAmount: 0.2 }),
      ],
      'Expense',
      2026,
    )
    const row = result.rows.find((r) => r.category === 'EssentialExpenses')!
    expect(row.totals[2026]).toBe(0.3)
  })

  it('ordena las filas por total acumulado descendente', () => {
    const result = aggregateByCategory(
      [
        tx({ type: 'Expense', idMainCategory: 'Luxuries', transactionDate: '2026-01-01', baseAmount: 40 }),
        tx({ type: 'Expense', idMainCategory: 'EssentialExpenses', transactionDate: '2026-01-01', baseAmount: 300 }),
        tx({ type: 'Expense', idMainCategory: 'Education', transactionDate: '2026-01-01', baseAmount: 120 }),
      ],
      'Expense',
      2026,
    )
    expect(result.rows.map((r) => r.category)).toEqual(
      ['EssentialExpenses', 'Education', 'Luxuries'],
    )
  })

  it('cada fila incluye los 4 años de la ventana (0 donde no hay datos)', () => {
    const result = aggregateByCategory(
      [
        tx({ type: 'Expense', idMainCategory: 'EssentialExpenses', transactionDate: '2025-06-01', baseAmount: 50 }),
      ],
      'Expense',
      2026,
    )
    const row = result.rows.find((r) => r.category === 'EssentialExpenses')!
    expect(Object.keys(row.totals).map(Number).sort()).toEqual([2023, 2024, 2025, 2026])
    expect(row.totals[2023]).toBe(0)
    expect(row.totals[2024]).toBe(0)
    expect(row.totals[2026]).toBe(0)
  })

  it('usa el año actual por defecto cuando no se pasa referenceYear', () => {
    const currentYear = new Date().getFullYear()
    const { years } = aggregateByCategory([], 'Expense')
    expect(years).toEqual([currentYear - 3, currentYear - 2, currentYear - 1, currentYear])
  })
})
