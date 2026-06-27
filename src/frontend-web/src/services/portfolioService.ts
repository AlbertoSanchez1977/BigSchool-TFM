import { api } from '@/lib/api'
import type {
  PortfolioListItem, Portfolio, PortfolioDetail,
  CreatePortfolioDto,
} from '@/types/portfolios'

// POST /portfolios solo acepta Name (CreatePortfolioRequest del controller).
// El backend asigna automáticamente la divisa del usuario como RealizedPnLCurrency.

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
}
