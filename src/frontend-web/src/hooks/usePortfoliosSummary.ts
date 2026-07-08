'use client'
import { useQuery } from '@tanstack/react-query'
import { portfolioService } from '@/services/portfolioService'
import { portfolioKeys } from '@/lib/queryKeys'

// GET /portfolios/summary — alimenta el mini-resumen de inversiones del Dashboard.
export function usePortfoliosSummary() {
  return useQuery({ queryKey: portfolioKeys.summary, queryFn: () => portfolioService.summary() })
}
