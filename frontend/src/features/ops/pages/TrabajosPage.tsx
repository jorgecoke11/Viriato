import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Card } from '../../../components/ui/Card'
import { Input } from '../../../components/ui/Input'
import * as opsApi from '../api'
import { StatusBadge } from '../StatusBadge'

const STATUSES = ['', 'Pending', 'Running', 'Completed', 'Failed']

export function TrabajosPage() {
  const [processCode, setProcessCode] = useState('')
  const [subjectKey, setSubjectKey] = useState('')
  const [status, setStatus] = useState('')

  const query = useQuery({
    queryKey: ['ops-trabajos', processCode, subjectKey, status],
    queryFn: () => opsApi.listTrabajos({ processCode, subjectKey, status }),
    refetchInterval: (q) => (q.state.data?.items.some((t) => t.status === 'Running') ? 3000 : false),
  })

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="page-title">Trabajos</h1>
        <p className="text-sm text-gray-500">
          Monitor genérico de cualquier scraping o script lanzado en la plataforma.
        </p>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <Input label="Proceso" name="processCode" value={processCode} onChange={(e) => setProcessCode(e.target.value)} placeholder="markets.sync…" />
        <Input label="Sujeto" name="subjectKey" value={subjectKey} onChange={(e) => setSubjectKey(e.target.value)} placeholder="AAPL…" />
        <div className="flex flex-col gap-1">
          <label htmlFor="status" className="text-sm font-medium text-gray-700">Estado</label>
          <select
            id="status"
            className="field"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
          >
            {STATUSES.map((s) => (
              <option key={s} value={s}>{s || 'Todos'}</option>
            ))}
          </select>
        </div>
      </div>

      <Card className="overflow-x-auto p-0">
        <table className="w-full min-w-[720px] text-left text-sm">
          <thead className="border-b border-gray-200 bg-gray-50/70">
            <tr>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">Proceso</th>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">Sujeto</th>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">Estado</th>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">Progreso</th>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">Resumen</th>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">Iniciado</th>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase" />
            </tr>
          </thead>
          <tbody>
            {query.data?.items.map((t) => (
              <tr key={t.id} className="border-b border-gray-100 last:border-0 hover:bg-gray-50/70">
                <td className="px-4 py-3 font-mono text-xs">{t.processCode}</td>
                <td className="px-4 py-3 text-gray-600">
                  {t.subjectType ? `${t.subjectType}: ${t.subjectKey}` : '—'}
                </td>
                <td className="px-4 py-3"><StatusBadge status={t.status} /></td>
                <td className="px-4 py-3 text-gray-600">{t.progress ?? '—'}{t.progress !== null ? '%' : ''}</td>
                <td className="px-4 py-3 text-gray-600">{t.summary ?? '—'}</td>
                <td className="px-4 py-3 text-gray-500">{new Date(t.startedAt).toLocaleString()}</td>
                <td className="px-4 py-3">
                  <Link to={`/ops/trabajos/${t.id}`} className="text-gray-500 hover:text-gray-900">
                    Ver
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {query.data?.items.length === 0 && <p className="p-4 text-sm text-gray-500">Sin trabajos todavía.</p>}
      </Card>
    </div>
  )
}
