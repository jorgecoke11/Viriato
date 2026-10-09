import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Button } from '../../../components/ui/Button'
import type { ColumnaDeTabla } from '../../../components/ui/DataTable'
import { ListaDeDatos } from '../../../components/ui/ListaDeDatos'
import { ApiError } from '../../../lib/apiClient'
import type { ConsultaDeLista } from '../../../lib/lista'
import { useToast } from '../../../lib/toast/useToast'
import { useListaPaginada } from '../../../lib/useListaPaginada'
import { useAuth } from '../../auth/useAuth'
import * as marketsApi from '../api'
import type { CompanyListItemDto } from '../api'
import { GrahamScoreBadge, SignalBadge } from '../SignalBadge'

const INDEXES = [
  { valor: '', etiqueta: 'Todos los índices' },
  { valor: 'Sp500', etiqueta: 'S&P 500' },
  { valor: 'Ndx100', etiqueta: 'Nasdaq-100' },
]

const obtenerId = (c: CompanyListItemDto) => c.ticker

const cargar = ({ busqueda, filtros, pagina, tamano }: ConsultaDeLista) =>
  marketsApi.listCompanies({
    search: busqueda,
    index: filtros.index || undefined,
    sector: filtros.sector || undefined,
    minScore: filtros.minScore ? Number(filtros.minScore) : undefined,
    page: pagina,
    pageSize: tamano,
  })

const columnas: ColumnaDeTabla<CompanyListItemDto>[] = [
  { clave: 'ticker', titulo: 'Ticker', celda: (c) => <span className="font-mono font-medium text-gray-900">{c.ticker}</span> },
  { clave: 'nombre', titulo: 'Nombre', celda: (c) => <span className="text-gray-700">{c.name}</span> },
  { clave: 'sector', titulo: 'Sector', celda: (c) => <span className="text-gray-600">{c.sector || '—'}</span> },
  { clave: 'graham', titulo: 'Graham', celda: (c) => <GrahamScoreBadge score={c.grahamScore} evaluated={c.criteriaEvaluated} /> },
  { clave: 'pe', titulo: 'P/E', celda: (c) => <span className="text-gray-600">{c.peRatio?.toFixed(1) ?? '—'}</span> },
  { clave: 'pb', titulo: 'P/B', celda: (c) => <span className="text-gray-600">{c.pbRatio?.toFixed(1) ?? '—'}</span> },
  {
    clave: 'margen',
    titulo: 'Margen seg.',
    celda: (c) => <span className="text-gray-600">{c.marginOfSafetyPercent !== null ? `${c.marginOfSafetyPercent.toFixed(0)}%` : '—'}</span>,
  },
  { clave: 'senal', titulo: 'Señal', celda: (c) => <SignalBadge signal={c.signal} /> },
]

export function CompaniesPage() {
  const { can } = useAuth()
  const navigate = useNavigate()
  const { showToast } = useToast()
  const [lastSyncTrabajoId, setLastSyncTrabajoId] = useState<string | null>(null)

  const fuente = useListaPaginada<CompanyListItemDto>({ clave: ['markets-companies'], cargar, obtenerId })

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
          <h1 className="page-title">Mercados</h1>
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

      <ListaDeDatos
        fuente={fuente}
        columnas={columnas}
        obtenerId={obtenerId}
        nombreDeFila={(c) => c.ticker}
        entidad={{ singular: 'compañía', plural: 'compañías' }}
        buscador={{ etiqueta: 'Buscar', placeholder: 'Ticker o nombre…' }}
        filtros={[
          { clave: 'index', etiqueta: 'Índice', opciones: INDEXES },
          { clave: 'sector', etiqueta: 'Sector', tipo: 'texto', placeholder: 'Sector: Technology…' },
          { clave: 'minScore', etiqueta: 'Score Graham mínimo', tipo: 'numero', placeholder: 'Graham mínimo (0–7)' },
        ]}
        vacio={{
          titulo: 'Todavía no hay compañías analizadas',
          descripcion: can('markets.manage') ? 'Lanza una sincronización para empezar.' : undefined,
        }}
        alHacerClic={(c) => navigate(`/mercados/${c.ticker}`)}
        anchoMinimo="760px"
      />
    </div>
  )
}
