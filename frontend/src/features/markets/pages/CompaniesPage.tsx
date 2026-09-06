import { useMutation, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Button } from '../../../components/ui/Button'
import { Card } from '../../../components/ui/Card'
import { Input } from '../../../components/ui/Input'
import { ApiError } from '../../../lib/apiClient'
import { useToast } from '../../../lib/toast/useToast'
import { useAuth } from '../../auth/useAuth'
import * as marketsApi from '../api'
import { GrahamScoreBadge, SignalBadge } from '../SignalBadge'

const INDEXES = [
  { value: '', label: 'Todos los índices' },
  { value: 'Sp500', label: 'S&P 500' },
  { value: 'Ndx100', label: 'Nasdaq-100' },
]

export function CompaniesPage() {
  const { can } = useAuth()
  const navigate = useNavigate()
  const { showToast } = useToast()

  const [search, setSearch] = useState('')
  const [index, setIndex] = useState('')
  const [sector, setSector] = useState('')
  const [minScore, setMinScore] = useState('')
  const [lastSyncTrabajoId, setLastSyncTrabajoId] = useState<string | null>(null)

  const query = useQuery({
    queryKey: ['markets-companies', search, index, sector, minScore],
    queryFn: () => marketsApi.listCompanies({
      search,
      index: index || undefined,
      sector: sector || undefined,
      minScore: minScore ? Number(minScore) : undefined,
    }),
  })

  const syncMutation = useMutation({
    mutationFn: (scope: marketsApi.SyncScope) => marketsApi.triggerSync(scope),
    onSuccess: (response) => {
      setLastSyncTrabajoId(response.trabajoId)
      showToast('success', 'Sincronización iniciada.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo iniciar la sincronización.'),
  })

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight text-gray-900">Mercados</h1>
          <p className="text-sm text-gray-500">Screener de compañías al estilo del inversor inteligente.</p>
        </div>
        {can('markets.manage') && (
          <div className="flex items-center gap-2">
            <Button variant="ghost" disabled={syncMutation.isPending} onClick={() => syncMutation.mutate('Sp500')}>
              Sincronizar S&amp;P 500
            </Button>
            <Button disabled={syncMutation.isPending} onClick={() => syncMutation.mutate('Ndx100')}>
              Sincronizar Nasdaq-100
            </Button>
          </div>
        )}
      </div>

      {lastSyncTrabajoId && (
        <div className="flex items-center justify-between rounded-md bg-blue-50 px-4 py-3 text-sm text-blue-700">
          <span>Sincronización en curso.</span>
          <Link to={`/ops/trabajos/${lastSyncTrabajoId}`} className="font-medium hover:underline">
            Ver progreso en vivo →
          </Link>
        </div>
      )}

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-4">
        <Input label="Buscar" name="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Ticker o nombre…" />
        <div className="flex flex-col gap-1">
          <label htmlFor="index" className="text-sm font-medium text-gray-700">Índice</label>
          <select
            id="index"
            className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
            value={index}
            onChange={(e) => setIndex(e.target.value)}
          >
            {INDEXES.map((opt) => (
              <option key={opt.value} value={opt.value}>{opt.label}</option>
            ))}
          </select>
        </div>
        <Input label="Sector" name="sector" value={sector} onChange={(e) => setSector(e.target.value)} placeholder="Technology…" />
        <Input label="Score Graham mínimo" name="minScore" type="number" min={0} max={7} value={minScore} onChange={(e) => setMinScore(e.target.value)} />
      </div>

      <Card className="overflow-x-auto p-0">
        <table className="w-full min-w-[760px] text-left text-sm">
          <thead className="border-b border-gray-200 text-gray-500">
            <tr>
              <th className="px-4 py-3 font-medium">Ticker</th>
              <th className="px-4 py-3 font-medium">Nombre</th>
              <th className="px-4 py-3 font-medium">Sector</th>
              <th className="px-4 py-3 font-medium">Graham</th>
              <th className="px-4 py-3 font-medium">P/E</th>
              <th className="px-4 py-3 font-medium">P/B</th>
              <th className="px-4 py-3 font-medium">Margen seg.</th>
              <th className="px-4 py-3 font-medium">Señal</th>
            </tr>
          </thead>
          <tbody>
            {query.data?.items.map((c) => (
              <tr
                key={c.ticker}
                className="cursor-pointer border-b border-gray-100 last:border-0 hover:bg-gray-50"
                onClick={() => navigate(`/mercados/${c.ticker}`)}
              >
                <td className="px-4 py-3 font-mono font-medium text-gray-900">{c.ticker}</td>
                <td className="px-4 py-3 text-gray-700">{c.name}</td>
                <td className="px-4 py-3 text-gray-600">{c.sector || '—'}</td>
                <td className="px-4 py-3"><GrahamScoreBadge score={c.grahamScore} evaluated={c.criteriaEvaluated} /></td>
                <td className="px-4 py-3 text-gray-600">{c.peRatio?.toFixed(1) ?? '—'}</td>
                <td className="px-4 py-3 text-gray-600">{c.pbRatio?.toFixed(1) ?? '—'}</td>
                <td className="px-4 py-3 text-gray-600">{c.marginOfSafetyPercent !== null ? `${c.marginOfSafetyPercent.toFixed(0)}%` : '—'}</td>
                <td className="px-4 py-3"><SignalBadge signal={c.signal} /></td>
              </tr>
            ))}
          </tbody>
        </table>
        {query.data?.items.length === 0 && (
          <p className="p-4 text-sm text-gray-500">
            Todavía no hay compañías analizadas. {can('markets.manage') ? 'Lanza una sincronización para empezar.' : ''}
          </p>
        )}
      </Card>
    </div>
  )
}
