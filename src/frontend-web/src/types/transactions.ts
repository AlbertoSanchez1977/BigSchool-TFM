export type TransactionType = 'income' | 'expense'

export interface Transaction {
  idTransaction: number
  idCategory: number
  categoryName: string
  type: TransactionType
  amount: number
  currency: string
  description: string
  transactionDate: string // ISO 8601
  createdAt: string
}

export interface CreateTransactionDto {
  idCategory: number
  type: TransactionType
  amount: number
  currency: string
  description: string
  transactionDate: string
}

export interface UpdateTransactionDto extends Partial<CreateTransactionDto> {}

export interface TransactionListItem {
  idTransaction: number
  idCategory: number
  categoryName: string
  type: TransactionType
  amount: number
  currency: string
  description: string
  transactionDate: string
}

export interface TransactionFilters {
  type?: TransactionType
  idCategory?: number
  from?: string
  to?: string
  page?: number
  pageSize?: number
}

export interface Summary {
  totalIncome: number
  totalExpense: number
  balance: number
  currency: string
  month: number
  year: number
}

export interface MonthlyChartPoint {
  year: number
  month: number
  totalIncome: number
  totalExpense: number
}
