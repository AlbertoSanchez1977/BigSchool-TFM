import { api } from '@/lib/api'
import type {
  PortfolioListItem, Portfolio, PortfolioDetail, PortfolioPerformance,
  CreatePortfolioDto, SellSharesDto, SellSharesResult, PagedPortfolios,
  RenamePortfolioDto, PortfoliosSummary,
} from '@/types/portfolios'
import type { PageMeta } from '@/types/pagination'

export const portfolioService = {

  // GET /portfolios?page&pageSize → PortfolioListItemDto[] (paginado)
  async list({ page, pageSize }: { page: number; pageSize: number }): Promise<PagedPortfolios> {
    const qs = new URLSearchParams({ page: String(page), pageSize: String(pageSize) }).toString()
    const result = await api.getWithMeta<PortfolioListItem[]>(`/portfolios?${qs}`)
    const m = result.meta as PageMeta
    return {
      items: result.data ?? [],
      meta: {
        page:       m?.page       ?? 1,
        pageSize:   m?.pageSize   ?? pageSize,
        totalCount: m?.totalCount ?? 0,
        totalPages: m?.totalPages ?? 1,
      },
    }
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

  // PUT /portfolios/{id} → sin data (ApiResponse.Success() vacío)
  async rename(id: number, data: RenamePortfolioDto): Promise<void> {
    await api.put<void>(`/portfolios/${id}`, data)
  },

  // DELETE /portfolios/{id} → 409 si hay holdings abiertos (guard fiscal, ver ApiError.message)
  async remove(id: number): Promise<void> {
    await api.delete<void>(`/portfolios/${id}`)
  },

  // GET /portfolios/summary → InvestmentsSummaryDto
  async summary(): Promise<PortfoliosSummary> {
    return api.get<PortfoliosSummary>('/portfolios/summary')
  },
}
