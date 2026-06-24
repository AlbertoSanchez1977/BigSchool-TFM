import type { Currency, MainCategory, TransactionType } from './enums'

// GET /api/v1/transactions → TransactionDto (multimoneda: original + convertido a base)
export interface Transaction {
  idTransaction: number
  type: TransactionType
  idMainCategory: MainCategory
  idSubCategory: number | null
  description: string | null
  transactionDate: string // DateOnly → "YYYY-MM-DD"
  originalAmount: number
  originalCurrency: Currency
  exchangeRate: number
  baseAmount: number
  baseCurrency: Currency
  rateDate: string // DateOnly → "YYYY-MM-DD"
}

// POST/PUT /api/v1/transactions (body del controller)
export interface CreateTransactionDto {
  type: TransactionType
  idMainCategory: MainCategory
  idSubCategory?: number | null
  description?: string | null
  transactionDate: string // "YYYY-MM-DD"
  amount: number
  currency: Currency
}

export type UpdateTransactionDto = CreateTransactionDto

// Filtros de GET /api/v1/transactions (query string)
export interface TransactionFilters {
  type?: TransactionType
  idMainCategory?: MainCategory
  from?: string
  to?: string
  page?: number
  pageSize?: number
}

// GET /api/v1/transactions/summary → TransactionSummaryDto
export interface Summary {
  totalIncome: number
  totalExpense: number
  balance: number
  baseCurrency: Currency
}

// GET /api/v1/transactions/monthly-chart → MonthlyChartPointDto[]
export interface MonthlyChartPoint {
  year: number
  month: number
  income: number
  expense: number
}
