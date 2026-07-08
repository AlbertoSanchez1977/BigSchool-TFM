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

export interface PagedValuations {
  items: ValuationListItem[]
  meta: PageMeta
}

// POST /companies/{id}/valuations → ValuationDto (comando: currency tipada como enum,
// a diferencia de ValuationListItem que es de Dapper y trae priceCurrency como string suelto).
export interface Valuation {
  idValuation: number
  idCompany: number
  price: number
  currency: Currency
  date: string
  source: string | null
}

// Body de POST /companies/{id}/valuations (AddValuationRequest). Sin currency: la hereda la
// empresa (Company.Currency) — el backend construye el Money con la moneda de la propia empresa.
export interface CreateValuationDto {
  price: number
  date: string          // "YYYY-MM-DD"
  source?: string | null
}

// GET /companies/{id}/valuations/series?period= → ValuationSeriesDto.
// Serie vacía (empresa sin valoraciones): points=[] y summary con todo a 0, currency="".
export interface ValuationSeriesPoint {
  date: string
  price: number
}

export interface ValuationSeriesSummary {
  first: number
  last: number
  min: number
  max: number
  changePct: number
}

export interface ValuationSeries {
  currency: string
  points: ValuationSeriesPoint[]
  summary: ValuationSeriesSummary
}
