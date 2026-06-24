export interface Company {
  idCompany: number
  name: string
  ticker: string
  currency: string
  sector: string | null
  market: string | null
}

export interface Valuation {
  idValuation: number
  idCompany: number
  price: number
  currency: string
  valuationDate: string
}
