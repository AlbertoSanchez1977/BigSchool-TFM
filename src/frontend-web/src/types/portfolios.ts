// Verificado contra PortfoliosController.cs y DTOs de BigSchool.Application (Task 8).
// Los DTOs de "query" (Dapper) devuelven Currency como string; los de "comando"
// la devuelven como enum (JsonStringEnumConverter → string con el nombre del miembro).

import type { Currency } from './enums'

// ── GET /portfolios → PortfolioListItemDto (Dapper: currency como string) ────────
export interface PortfolioListItem {
  idPortfolio: number
  name: string
  realizedPnL: number
  realizedPnLCurrency: string
  marketValue: number
  costBasis: number
  unrealizedPnL: number
  totalPnL: number
}

// ── POST /portfolios → PortfolioDto (comando: currency tipada) ────────────────────
export interface Portfolio {
  idPortfolio: number
  name: string
  realizedPnL: number
  realizedPnLCurrency: Currency
}

// ── GET /portfolios/{id} → PortfolioDetailDto ─────────────────────────────────────
export interface PortfolioDetail {
  idPortfolio: number
  name: string
  realizedPnL: number
  realizedPnLCurrency: string
  holdings: HoldingListItem[]
}

// Body de POST /portfolios — solo Name (el backend derive la divisa del usuario)
export interface CreatePortfolioDto {
  name: string
}

// ── Holdings en GET /portfolios/{id} → HoldingListItemDto (Dapper) ───────────────
export interface HoldingListItem {
  idHolding: number
  idCompany: number
  ticker: string
  companyCurrency: string
  shares: number
  openShares: number
  buyOriginalAmount: number
  buyOriginalCurrency: string
  buyExchangeRate: number
  buyBaseAmount: number
  buyBaseCurrency: string
  buyRateDate: string         // "YYYY-MM-DD"
  buyDate: string             // "YYYY-MM-DD"
  notes: string | null
  marketValue: number
  costBasis: number
  unrealizedPnL: number
}

// ── POST/PUT /portfolios/{id}/holdings → HoldingDto (comando: currency tipada) ────
export interface HoldingDto {
  idHolding: number
  idCompany: number
  shares: number
  buyOriginalAmount: number
  buyOriginalCurrency: Currency
  buyExchangeRate: number
  buyBaseAmount: number
  buyBaseCurrency: Currency
  buyRateDate: string
  buyDate: string
  notes: string | null
}

// Body de POST /portfolios/{id}/holdings (AddHoldingRequest del controller)
export interface AddHoldingDto {
  idCompany: number
  shares: number
  buyPrice: number            // campo real: BuyPrice (no buyPriceLocal)
  buyDate: string
  notes?: string | null
}

// Body de PUT /portfolios/{id}/holdings/{holdingId} (UpdateHoldingRequest)
export interface UpdateHoldingNotesDto {
  notes: string | null
}

// ── GET /portfolios/{id}/performance → PortfolioPerformanceDto ───────────────────
export interface PortfolioPerformance {
  idPortfolio: number
  name: string
  baseCurrency: string
  marketValue: number
  costBasis: number
  unrealizedPnL: number
  realizedPnL: number
  totalPnL: number
  returnPct: number
  holdings: HoldingPerformance[]
}

// Elemento de HoldingPerformanceDto (sin companyName — solo ticker)
// TODO (deuda técnica): el endpoint GET /portfolios/{id}/performance debe añadir los campos
// de moneda original para evitar que el frontend los calcule a partir del detalle de cartera.
// Ver BigSchool.Application.DTOs.Investments.HoldingPerformanceDto — pending backend iteration.
export interface HoldingPerformance {
  idHolding: number
  idCompany: number
  ticker: string
  openShares: number
  costBasis: number
  marketValue: number
  unrealizedPnL: number
  unrealizedPnLPct: number           // campo real: UnrealizedPnLPct (≠ returnPct)
  // Campos pendientes de backend — undefined hasta que el endpoint los materialice
  buyOriginalCurrency?: string
  costBasisOriginal?: number         // coste de acciones abiertas en moneda original
  marketValueOriginal?: number       // valor de mercado en moneda original
  unrealizedPnLOriginal?: number     // PnL no realizado en moneda original
}

// ── Body de POST /portfolios/{id}/sales (SellSharesRequest) ──────────────────────
export interface SellSharesDto {
  companyId: number           // campo real: CompanyId (no idCompany)
  shares: number
  sellPrice: number           // campo real: SellPrice (no sellPriceLocal)
  sellDate: string
  notes?: string | null
}

// ── Response de POST /portfolios/{id}/sales → SellSharesResultDto ────────────────
export interface SellSharesResult {
  disposals: Disposal[]
  portfolioRealizedPnL: number
  realizedPnLCurrency: Currency
}

// ── DisposalDto (dentro de SellSharesResultDto) ───────────────────────────────────
export interface Disposal {
  idDisposal: number
  idHolding: number
  shares: number
  sellOriginalAmount: number
  sellOriginalCurrency: Currency
  sellExchangeRate: number
  sellBaseAmount: number
  sellBaseCurrency: Currency
  sellRateDate: string
  sellDate: string
  realizedPnL: number
  realizedPnLCurrency: Currency
  notes: string | null
}
