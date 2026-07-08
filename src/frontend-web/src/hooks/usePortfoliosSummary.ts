'use client'
import { useQuery } from '@tanstack/react-query'
import { portfolioService } from '@/services/portfolioService'

// GET /portfolios/summary — alimenta el mini-resumen de inversiones del Dashboard.
export function usePortfoliosSummary() {
  return useQuery({ queryKey: ['portfolios', 'summary'], queryFn: () => portfolioService.summary() })
}
