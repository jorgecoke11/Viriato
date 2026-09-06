import { apiFetch } from '../../lib/apiClient'
import type { PagedResult } from '../../lib/types'

export type CriterionResult = 'Pass' | 'Fail' | 'NotApplicable'

export interface GrahamCriterionDto {
  code: string
  label: string
  value: string | null
  result: CriterionResult
}

export interface FundamentalsYearDto {
  fiscalYear: number
  eps: number | null
  bookValuePerShare: number | null
  dividendPerShare: number | null
  currentAssets: number | null
  currentLiabilities: number | null
  sharesOutstanding: number | null
}

export interface PricePointDto {
  date: string
  close: number
}

export type MarketSignal = 'Bullish' | 'Bearish' | 'Neutral'

export interface CompanyAnalysisDto {
  ticker: string
  name: string
  sector: string
  industry: string
  grahamScore: number
  criteriaEvaluated: number
  criteria: GrahamCriterionDto[]
  peRatio: number | null
  pbRatio: number | null
  grahamNumber: number | null
  intrinsicValue: number | null
  marginOfSafetyPercent: number | null
  sma50: number | null
  sma200: number | null
  rsi14: number | null
  signal: MarketSignal
  priceAtComputation: number
  fundamentalsHistory: FundamentalsYearDto[]
  recentPrices: PricePointDto[]
  computedAt: string
}

export interface CompanyDetailDto {
  trabajoId: string
  analysis: CompanyAnalysisDto
}

export interface CompanyListItemDto {
  ticker: string
  name: string
  sector: string
  grahamScore: number
  criteriaEvaluated: number
  peRatio: number | null
  pbRatio: number | null
  marginOfSafetyPercent: number | null
  signal: string
  computedAt: string
}

export interface ListCompaniesFilters {
  search?: string
  index?: string
  sector?: string
  minScore?: number
}

export type SyncScope = 'Sp500' | 'Ndx100'

const buildQuery = (filters: ListCompaniesFilters) => {
  const params = new URLSearchParams()
  if (filters.search) params.set('search', filters.search)
  if (filters.index) params.set('index', filters.index)
  if (filters.sector) params.set('sector', filters.sector)
  if (filters.minScore !== undefined) params.set('minScore', String(filters.minScore))
  const query = params.toString()
  return query ? `?${query}` : ''
}

export const listCompanies = (filters: ListCompaniesFilters = {}) =>
  apiFetch<PagedResult<CompanyListItemDto>>(`/markets/companies${buildQuery(filters)}`)

export const getCompany = (ticker: string) => apiFetch<CompanyDetailDto>(`/markets/companies/${ticker}`)

export const triggerSync = (scope: SyncScope) =>
  apiFetch<{ trabajoId: string }>('/markets/sync', { method: 'POST', body: JSON.stringify({ scope }) })
