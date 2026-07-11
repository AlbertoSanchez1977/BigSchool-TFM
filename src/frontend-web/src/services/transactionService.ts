import { api } from '@/lib/api'
import type {
  TransactionListItem, Transaction, TransactionFilters,
  PagedTransactions, Summary, MonthlyChartPoint, CategoryTotal,
  CreateTransactionDto, UpdateTransactionDto,
} from '@/types/transactions'
import type { TransactionType, MainCategory, Currency } from '@/types/enums'

// ── Mapeo numérico → string enum ──────────────────────────────────────────────
// El GET /transactions usa Dapper y devuelve TransactionListItemDto con tipos
// primitivos (short, int) en lugar de los enums.  Mapeamos aquí para que la
// UI trabaje siempre con los string unions tipados.
// Equivale a un Mapper en la capa de aplicación C#.

const TYPE_MAP: Record<number, TransactionType> = {
  0: 'Income',
  1: 'Expense',
}

const CATEGORY_MAP: Record<number, MainCategory> = {
  1:  'EssentialExpenses',
  2:  'Investment',
  3:  'Savings',
  4:  'Donations',
  5:  'Luxuries',
  6:  'Education',
  7:  'Amortizations',
  10: 'Salary',
  11: 'Rentals',
  12: 'Dividends',
  13: 'Other',
}

function mapItem(raw: TransactionListItem): Transaction {
  return {
    idTransaction:    raw.idTransaction,
    type:             TYPE_MAP[raw.type] ?? 'Expense',
    idMainCategory:   CATEGORY_MAP[raw.idMainCategory] ?? 'Other',
    idSubCategory:    raw.idSubCategory,
    description:      raw.description,
    transactionDate:  raw.transactionDate,
    originalAmount:   raw.originalAmount,
    originalCurrency: raw.originalCurrency as Currency,
    exchangeRate:     raw.exchangeRate,
    baseAmount:       raw.baseAmount,
    baseCurrency:     raw.baseCurrency as Currency,
    rateDate:         raw.rateDate,
  }
}

// ── Query string builder ──────────────────────────────────────────────────────

function buildParams(filters: TransactionFilters): string {
  const p = new URLSearchParams()
  if (filters.type)            p.set('type',      filters.type)
  if (filters.idMainCategory)  p.set('category',  filters.idMainCategory)
  if (filters.from)            p.set('from',      filters.from)
  if (filters.to)              p.set('to',        filters.to)
  if (filters.page)            p.set('page',      String(filters.page))
  if (filters.pageSize)        p.set('pageSize',  String(filters.pageSize))
  return p.toString()
}

// ── Service ───────────────────────────────────────────────────────────────────

export const transactionService = {

  async list(filters: TransactionFilters): Promise<PagedTransactions> {
    const qs = buildParams(filters)
    const path = qs ? `/transactions?${qs}` : '/transactions'
    const result = await api.getWithMeta<TransactionListItem[]>(path)

    // El backend siempre devuelve meta de paginación para este endpoint
    const m = result.meta as { page: number; pageSize: number; totalCount: number; totalPages: number }

    return {
      items: (result.data ?? []).map(mapItem),
      meta: {
        page:       m?.page       ?? 1,
        pageSize:   m?.pageSize   ?? 20,
        totalCount: m?.totalCount ?? 0,
        totalPages: m?.totalPages ?? 1,
      },
    }
  },

  async summary(from: string, to: string): Promise<Summary> {
    const qs = new URLSearchParams({ from, to }).toString()
    return api.get<Summary>(`/transactions/summary?${qs}`)
  },

  // GET /transactions/monthly-chart?year=N → MonthlyChartPointDto[]
  async monthlyChart(year: number): Promise<MonthlyChartPoint[]> {
    return api.get<MonthlyChartPoint[]>(`/transactions/monthly-chart?year=${year}`)
  },

  // GET /transactions/by-category?from&to&type → CategoryTotalDto[] (agregado en SQL)
  async byCategory(from: string, to: string, type: TransactionType): Promise<CategoryTotal[]> {
    const qs = new URLSearchParams({ from, to, type }).toString()
    return api.get<CategoryTotal[]>(`/transactions/by-category?${qs}`)
  },

  // GET /transactions/monthly?from&to&category&type → MonthlyChartPointDto[]
  async monthly(from: string, to: string, type: TransactionType, category?: MainCategory): Promise<MonthlyChartPoint[]> {
    const p = new URLSearchParams({ from, to, type })
    if (category) p.set('category', category)
    return api.get<MonthlyChartPoint[]>(`/transactions/monthly?${p.toString()}`)
  },

  // POST /transactions — devuelve TransactionDto (enums como string, sin necesidad de mapeo numérico)
  async create(data: CreateTransactionDto): Promise<Transaction> {
    return api.post<Transaction>('/transactions', data)
  },

  // PUT /transactions/{id}
  async update(id: number, data: UpdateTransactionDto): Promise<Transaction> {
    return api.put<Transaction>(`/transactions/${id}`, data)
  },

  // DELETE /transactions/{id}
  async delete(id: number): Promise<void> {
    await api.delete<void>(`/transactions/${id}`)
  },
}
