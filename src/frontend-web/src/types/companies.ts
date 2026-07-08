import type { PageMeta } from './pagination'
import type { Currency, Sector, Market } from './enums'

// Verificado contra CompaniesController.cs y DTOs de BigSchool.Application (Task 8).
// CompanyListItemDto usa Dapper → currency como string (no enum).

// GET /companies (paginado) y GET /companies/{id} → misma CompanyListItemDto para ambos.
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

export interface PagedCompanies {
  items: CompanyListItem[]
  meta: PageMeta
}

// POST /companies → CompanyDto (comando: sector/market/currency tipados, sin lastPrice
// ni lastValuationDate — una empresa recién creada no tiene valoraciones aún).
export interface Company {
  idCompany: number
  name: string
  ticker: string
  sector: Sector | null
  market: Market | null
  currency: Currency
}

// Body de POST /companies (CreateCompanyRequest). sector/market son opcionales
// (Sector?/Market? en el backend); name, ticker y currency son obligatorios.
export interface CreateCompanyDto {
  name: string
  ticker: string
  sector?: Sector | null
  market?: Market | null
  currency: Currency
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
