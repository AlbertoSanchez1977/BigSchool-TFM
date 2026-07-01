import { api } from '@/lib/api'
import type {
  PortfolioListItem, Portfolio, PortfolioDetail, PortfolioPerformance,
  CreatePortfolioDto, SellSharesDto, SellSharesResult,
} from '@/types/portfolios'

export const portfolioService = {

  // GET /portfolios → PortfolioListItemDto[]
  async list(): Promise<PortfolioListItem[]> {
    return api.get<PortfolioListItem[]>('/portfolios')
  },

  // GET /portfolios/{id} → PortfolioDetailDto
  async getById(id: number): Promise<PortfolioDetail> {
    return api.get<PortfolioDetail>(`/portfolios/${id}`)
  },

  // POST /portfolios → PortfolioDto (comando)
  async create(data: CreatePortfolioDto): Promise<Portfolio> {
    return api.post<Portfolio>('/portfolios', data)
  },

  // GET /portfolios/{id}/performance → PortfolioPerformanceDto
  async performance(id: number): Promise<PortfolioPerformance> {
    return api.get<PortfolioPerformance>(`/portfolios/${id}/performance`)
  },

  // POST /portfolios/{id}/sales → SellSharesResultDto (SellSharesRequest del controller)
  async sellShares(id: number, data: SellSharesDto): Promise<SellSharesResult> {
    return api.post<SellSharesResult>(`/portfolios/${id}/sales`, data)
  },
}
