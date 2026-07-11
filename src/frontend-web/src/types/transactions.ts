import type { Currency, MainCategory, TransactionType } from './enums'
import type { PageMeta } from './pagination'

export type { PageMeta } from './pagination'

// ── Raw shape of TransactionListItemDto (GET /transactions, GET /transactions/{id}) ──
// El backend usa Dapper para leer directamente de BD:
//   - type: short  (0=Income, 1=Expense)  — NO pasa por JsonStringEnumConverter
//   - idMainCategory: int  (1=EssentialExpenses, etc.)  — ídem
// TransactionDto (POST/PUT response) SÍ usa los enums con JsonStringEnumConverter.
export interface TransactionListItem {
  idTransaction: number
  type: number          // 0 = Income, 1 = Expense
  idMainCategory: number  // valores numéricos de MainCategory
  idSubCategory: number | null
  description: string | null
  transactionDate: string // "YYYY-MM-DD"
  originalAmount: number
  originalCurrency: string // ya es string en el DTO
  exchangeRate: number
  baseAmount: number
  baseCurrency: string
  rateDate: string
}

// ── Tipo de dominio con enums tipados (lo que usa la UI) ──
// El service hace el mapping TransactionListItem → Transaction.
export interface Transaction {
  idTransaction: number
  type: TransactionType     // 'Income' | 'Expense'
  idMainCategory: MainCategory  // 'EssentialExpenses' | …
  idSubCategory: number | null
  description: string | null
  transactionDate: string
  originalAmount: number
  originalCurrency: Currency
  exchangeRate: number
  baseAmount: number
  baseCurrency: Currency
  rateDate: string
}

// ── Paginación ────────────────────────────────────────────────────────────────
// PageMeta vive en types/pagination.ts (compartido con portfolios/companies/valuations);
// se reexporta aquí para no romper los imports existentes.

export interface PagedTransactions {
  items: Transaction[]
  meta: PageMeta
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

// GET /api/v1/transactions/by-category → CategoryTotalDto (Dapper: mainCategory YA viene
// como nombre string — el handler hace `((MainCategory)IdMainCategory).ToString()`).
export interface CategoryTotal {
  idMainCategory: number
  mainCategory: MainCategory
  total: number
}
