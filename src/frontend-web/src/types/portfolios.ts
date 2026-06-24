export interface PortfolioListItem {
  idPortfolio: number
  name: string
  baseCurrency: string
  totalMarketValue: number
  totalRealizedPnL: number
  createdAt: string
}

export interface PortfolioDetail extends PortfolioListItem {
  holdings: Holding[]
}

export interface CreatePortfolioDto {
  name: string
  baseCurrency: string
}

export interface Holding {
  idHolding: number
  idPortfolio: number
  idCompany: number
  companyName: string
  companyCurrency: string
  companyTicker: string
  shares: number
  openShares: number
  buyPriceLocal: number  // precio de compra en moneda de la empresa
  buyPriceBase: number   // precio de compra convertido a moneda base (snapshot FX)
  buyDate: string
  notes: string | null
  disposals: Disposal[]
}

export interface CreateHoldingDto {
  idCompany: number
  shares: number
  buyPriceLocal: number
  buyDate: string
  notes?: string
}

export interface UpdateHoldingNotesDto {
  notes: string
}

export interface Disposal {
  idDisposal: number
  idHolding: number
  shares: number
  sellPriceLocal: number
  sellPriceBase: number
  sellDate: string
  realizedPnL: number
}

export interface SellDto {
  idCompany: number   // venta FIFO a nivel empresa dentro de la cartera
  shares: number
  sellPriceLocal: number
  sellDate: string
}

export interface SellResult {
  disposals: Disposal[]
  totalRealizedPnL: number
}

export interface Performance {
  totalMarketValue: number
  totalCostBase: number
  totalUnrealizedPnL: number
  totalRealizedPnL: number
  totalPnL: number
  returnPct: number
  currency: string
  holdingPerformances: HoldingPerformance[]
}

export interface HoldingPerformance {
  idHolding: number
  companyName: string
  companyTicker: string
  openShares: number
  costBase: number
  marketValue: number
  unrealizedPnL: number
  returnPct: number
}
