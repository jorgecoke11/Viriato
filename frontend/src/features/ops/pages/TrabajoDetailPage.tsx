import { useQuery } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { Card } from '../../../components/ui/Card'
import * as opsApi from '../api'
import { StatusBadge } from '../StatusBadge'

const levelStyles: Record<string, string> = {
  Info: 'text-gray-700',
  Warning: 'text-amber-700',
  Error: 'text-red-700',
}

export function TrabajoDetailPage() {
  const { id } = useParams<{ id: string }>()

  const query = useQuery({
    queryKey: ['ops-trabajo', id],
    queryFn: () => opsApi.getTrabajo(id!),
    enabled: Boolean(id),
    refetchInterval: (q) => (q.state.data?.status === 'Running' ? 2000 : false),
  })

  if (query.isLoading) return <p className="text-gray-500">Cargando…</p>
  if (!query.data) return <p className="text-gray-500">Trabajo no encontrado.</p>

  const trabajo = query.data
  let prettyData: string | null = null
  if (trabajo.data) {
    try {
      prettyData = JSON.stringify(JSON.parse(trabajo.data), null, 2)
    } catch {
      prettyData = trabajo.data
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <Link to="/ops/trabajos" className="text-sm text-gray-500 hover:text-gray-900">
        ← Trabajos
      </Link>

      <Card>
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h1 className="font-mono text-lg font-semibold text-gray-900">{trabajo.processCode}</h1>
            <p className="text-sm text-gray-500">
              {trabajo.subjectType ? `${trabajo.subjectType}: ${trabajo.subjectKey}` : 'Sin sujeto'}
            </p>
          </div>
          <StatusBadge status={trabajo.status} />
        </div>

        <div className="mt-4 grid grid-cols-2 gap-4 text-sm sm:grid-cols-4">
          <div>
            <div className="text-gray-500">Progreso</div>
            <div className="font-medium text-gray-900">{trabajo.progress ?? '—'}{trabajo.progress !== null ? '%' : ''}</div>
          </div>
          <div>
            <div className="text-gray-500">Iniciado</div>
            <div className="font-medium text-gray-900">{new Date(trabajo.startedAt).toLocaleString()}</div>
          </div>
          <div>
            <div className="text-gray-500">Finalizado</div>
            <div className="font-medium text-gray-900">{trabajo.finishedAt ? new Date(trabajo.finishedAt).toLocaleString() : '—'}</div>
          </div>
          <div>
            <div className="text-gray-500">Resumen</div>
            <div className="font-medium text-gray-900">{trabajo.summary ?? '—'}</div>
          </div>
        </div>

        {trabajo.error && (
          <p className="mt-4 rounded-md bg-red-50 px-3 py-2 text-sm text-red-700">{trabajo.error}</p>
        )}
      </Card>

      {trabajo.children.length > 0 && (
        <Card className="overflow-x-auto p-0">
          <h2 className="px-4 pt-4 text-sm font-medium text-gray-700">Trabajos hijos ({trabajo.children.length})</h2>
          <table className="mt-2 w-full min-w-[560px] text-left text-sm">
            <thead className="border-b border-gray-200 text-gray-500">
              <tr>
                <th className="px-4 py-3 font-medium">Sujeto</th>
                <th className="px-4 py-3 font-medium">Estado</th>
                <th className="px-4 py-3 font-medium">Resumen</th>
                <th className="px-4 py-3 font-medium" />
              </tr>
            </thead>
            <tbody>
              {trabajo.children.map((child) => (
                <tr key={child.id} className="border-b border-gray-100 last:border-0">
                  <td className="px-4 py-3 text-gray-600">{child.subjectKey ?? '—'}</td>
                  <td className="px-4 py-3"><StatusBadge status={child.status} /></td>
                  <td className="px-4 py-3 text-gray-600">{child.summary ?? '—'}</td>
                  <td className="px-4 py-3">
                    <Link to={`/ops/trabajos/${child.id}`} className="text-gray-500 hover:text-gray-900">Ver</Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}

      <Card>
        <h2 className="mb-2 text-sm font-medium text-gray-700">Logs</h2>
        {trabajo.logs.length === 0 ? (
          <p className="text-sm text-gray-500">Sin logs todavía.</p>
        ) : (
          <ul className="flex flex-col gap-1 font-mono text-xs">
            {trabajo.logs.map((e) => (
              <li key={e.id} className={levelStyles[e.level] ?? 'text-gray-700'}>
                <span className="text-gray-400">{new Date(e.timestamp).toLocaleTimeString()}</span> {e.message}
              </li>
            ))}
          </ul>
        )}
      </Card>

      {prettyData && (
        <Card>
          <h2 className="mb-2 text-sm font-medium text-gray-700">Datos</h2>
          <pre className="overflow-x-auto rounded-md bg-gray-50 p-3 text-xs text-gray-700">{prettyData}</pre>
        </Card>
      )}
    </div>
  )
}
