import { api } from '@/lib/api'
import type {
  PortfolioDetail, HoldingDto, AddHoldingDto, UpdateHoldingNotesDto,
} from '@/types/portfolios'
import type { CompanyListItem } from '@/types/companies'

export const holdingsService = {

  // GET /portfolios/{id} → PortfolioDetailDto (cartera + lista de holdings)
  async getPortfolio(id: number): Promise<PortfolioDetail> {
    return api.get<PortfolioDetail>(`/portfolios/${id}`)
  },

  // GET /companies?pageSize=100 (para el combobox de "añadir holding").
  // Deuda documentada: sin ?search server-side, el combobox filtra en cliente sobre
  // este máximo de 100 — si el catálogo lo supera, el filtro queda incompleto (spec 011 §6).
  async listCompanies(): Promise<CompanyListItem[]> {
    return api.get<CompanyListItem[]>('/companies?pageSize=100')
  },

  // POST /portfolios/{id}/holdings → HoldingDto (AddHoldingRequest)
  async addHolding(portfolioId: number, data: AddHoldingDto): Promise<HoldingDto> {
    return api.post<HoldingDto>(`/portfolios/${portfolioId}/holdings`, data)
  },

  // PUT /portfolios/{id}/holdings/{holdingId} → HoldingDto (UpdateHoldingRequest)
  async updateNotes(portfolioId: number, holdingId: number, data: UpdateHoldingNotesDto): Promise<HoldingDto> {
    return api.put<HoldingDto>(`/portfolios/${portfolioId}/holdings/${holdingId}`, data)
  },

  // DELETE /portfolios/{id}/holdings/{holdingId}
  async deleteHolding(portfolioId: number, holdingId: number): Promise<void> {
    await api.delete<void>(`/portfolios/${portfolioId}/holdings/${holdingId}`)
  },
}
