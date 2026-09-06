import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { Button } from '../../../components/ui/Button'
import { Card } from '../../../components/ui/Card'
import { Input } from '../../../components/ui/Input'
import { useAuth } from '../../auth/useAuth'
import * as casosApi from '../api'
import { CasoEstadoBadge } from '../CasoEstadoBadge'

const ESTADOS = ['', 'Iniciado', 'EnProgreso', 'Pausado', 'EsperandoRevisionHumana', 'Completado', 'Fallido', 'Cancelado']

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
          <h1 className="text-2xl font-semibold tracking-tight text-gray-900">Casos</h1>
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
            className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
            value={estado}
            onChange={(e) => setEstado(e.target.value)}
          >
            {ESTADOS.map((e) => (
              <option key={e} value={e}>{e || 'Todos'}</option>
            ))}
          </select>
        </div>
      </div>

      <Card className="overflow-x-auto p-0">
        <table className="w-full min-w-[720px] text-left text-sm">
          <thead className="border-b border-gray-200 text-gray-500">
            <tr>
              <th className="px-4 py-3 font-medium">Título</th>
              <th className="px-4 py-3 font-medium">Estado</th>
              <th className="px-4 py-3 font-medium">Creado</th>
              <th className="px-4 py-3 font-medium">Finalizado</th>
            </tr>
          </thead>
          <tbody>
            {query.data?.items.map((c) => (
              <tr
                key={c.id}
                className="cursor-pointer border-b border-gray-100 last:border-0 hover:bg-gray-50"
                onClick={() => navigate(`/casos/${c.id}`)}
              >
                <td className="px-4 py-3 font-medium text-gray-900">{c.titulo}</td>
                <td className="px-4 py-3"><CasoEstadoBadge estado={c.estado} /></td>
                <td className="px-4 py-3 text-gray-500">{new Date(c.createdAt).toLocaleString()}</td>
                <td className="px-4 py-3 text-gray-500">{c.completedAt ? new Date(c.completedAt).toLocaleString() : '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>

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
