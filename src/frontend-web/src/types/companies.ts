// Verificado contra CompaniesController.cs y DTOs de BigSchool.Application (Task 8).
// CompanyListItemDto usa Dapper → currency como string (no enum).

// GET /companies → CompanyListItemDto (Dapper)
export interface CompanyListItem {
  idCompany: number
  name: string
  ticker: string
  sector: string | null
  market: string | null
  currency: string
  lastPrice: number | null
  lastValuationDate: string | null   // "YYYY-MM-DD"
}

// GET /companies/{id}/valuations → ValuationListItemDto (Dapper)
export interface ValuationListItem {
  idValuation: number
  idCompany: number
  price: number
  priceCurrency: string
  date: string           // "YYYY-MM-DD"
  source: string | null
}
