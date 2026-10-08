import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { Button } from '../../../components/ui/Button'
import { Card } from '../../../components/ui/Card'
import { Input } from '../../../components/ui/Input'
import { useAuth } from '../../auth/useAuth'
import * as casosApi from '../api'
import { CasosTabla } from '../CasosTabla'

const ESTADOS: { value: string; label: string }[] = [
  { value: '', label: 'Todos' },
  { value: 'Iniciado', label: 'Iniciado' },
  { value: 'EnProgreso', label: 'En progreso' },
  { value: 'Pausado', label: 'Pausado' },
  { value: 'EsperandoRevisionHumana', label: 'Esperando revisión' },
  { value: 'Completado', label: 'Completado' },
  { value: 'Fallido', label: 'Fallido' },
  { value: 'Cancelado', label: 'Cancelado' },
]

export function CasosListPage() {
  const navigate = useNavigate()
  const { can } = useAuth()
  const [searchParams, setSearchParams] = useSearchParams()
  const flujoId = searchParams.get('flujoId') ?? ''

  const [estado, setEstado] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)

  const flujosQuery = useQuery({ queryKey: ['flujos-asignados'], queryFn: casosApi.listFlujosAsignados })
  const flujoNombre = flujosQuery.data?.find((f) => f.id === flujoId)?.nombre

  const query = useQuery({
    queryKey: ['casos', flujoId, estado, search, page],
    queryFn: () => casosApi.listCasos({ flujoId: flujoId || undefined, estado: estado || undefined, search: search || undefined, page }),
    refetchInterval: (q) => (q.state.data?.items.some((c) => c.estado === 'EnProgreso') ? 3000 : false),
  })

  const clearFlujo = () => {
    searchParams.delete('flujoId')
    setSearchParams(searchParams)
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="page-title">Casos</h1>
          {flujoId && (
            <p className="text-sm text-gray-500">
              Filtrando por flujo: <span className="font-medium text-gray-700">{flujoNombre ?? flujoId}</span>{' '}
              <button className="text-gray-400 hover:text-gray-600" onClick={clearFlujo}>
                (quitar filtro)
              </button>
            </p>
          )}
        </div>
        {can('casos.manage') && (
          <Button onClick={() => navigate('/casos/nuevo')}>Nuevo caso</Button>
        )}
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <Input label="Buscar" name="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Título del caso…" />
        <div className="flex flex-col gap-1">
          <label htmlFor="estado" className="text-sm font-medium text-gray-700">Estado</label>
          <select
            id="estado"
            className="field"
            value={estado}
            onChange={(e) => setEstado(e.target.value)}
          >
            {ESTADOS.map((e) => (
              <option key={e.value} value={e.value}>{e.label}</option>
            ))}
          </select>
        </div>
      </div>

      <Card className="overflow-hidden p-0">
        {query.data && query.data.items.length > 0 && <CasosTabla casos={query.data.items} />}

        {query.isLoading && <p className="p-4 text-sm text-gray-500">Cargando casos…</p>}

        {query.isError && (
          <div className="flex items-center justify-between p-4 text-sm">
            <span className="text-red-600">No se han podido cargar los casos.</span>
            <button className="font-medium text-gray-700 hover:text-gray-900" onClick={() => query.refetch()}>
              Reintentar
            </button>
          </div>
        )}

        {query.isSuccess && query.data.items.length === 0 && (
          <p className="p-4 text-sm text-gray-500">No hay casos con estos filtros.</p>
        )}
      </Card>

      {query.data && query.data.total > query.data.pageSize && (
        <div className="flex items-center justify-between text-sm text-gray-500">
          <button
            className="disabled:opacity-40"
            disabled={page <= 1}
            onClick={() => setPage((p) => Math.max(1, p - 1))}
          >
            ← Anterior
          </button>
          <span>Página {page}</span>
          <button
            className="disabled:opacity-40"
            disabled={page * query.data.pageSize >= query.data.total}
            onClick={() => setPage((p) => p + 1)}
          >
            Siguiente →
          </button>
        </div>
      )}
    </div>
  )
}
