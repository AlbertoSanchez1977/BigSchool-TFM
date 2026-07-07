import type { MainCategory, TransactionType } from '@/types/enums'

// Derivado del rango del enum backend (Domain/Finance/Enums/MainCategory.cs):
// 1-7 = gasto, 10-13 = ingreso.
export const CATEGORIES_BY_TYPE: Record<TransactionType, MainCategory[]> = {
  Expense: ['EssentialExpenses', 'Investment', 'Savings', 'Donations', 'Luxuries', 'Education', 'Amortizations'],
  Income:  ['Salary', 'Rentals', 'Dividends', 'Other'],
}
