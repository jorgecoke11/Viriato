import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { ApiError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import * as rpaApi from '../rpa/api'
import * as flujosApi from './api'
import type { FlujoPasoDefInput, TipoPaso } from './api'

const tiposPaso: TipoPaso[] = ['Rpa', 'Agente', 'Api', 'Interno', 'Decision', 'Espera', 'RevisionHumana']

const configuracionHint: Record<TipoPaso, string> = {
  Rpa: '{"aplicacion": "...", "script": "...", "timeoutSeconds": 300}',
  Agente: 'Libre — lo interpreta el propio agente.',
  Api: '{"url": "...", "method": "GET", "headersTemplate": {...}}',
  Decision: '{"condicion": "...", "ordenSiVerdadero": 1, "ordenSiFalso": 2}',
  Espera: '{"duracionMinutos": 60} o {"condicionReanudacion": "..."}',
  RevisionHumana: '{"instrucciones": "..."}',
  Interno: 'Libre — lo interpreta el propio ejecutor.',
}

interface PasoRow extends FlujoPasoDefInput {
  key: string
}

let nextKey = 0
const newRow = (orden: number): PasoRow => ({
  key: `nuevo-${++nextKey}`,
  orden,
  nombre: '',
  tipoPaso: 'Interno',
  agenteDefinicionId: null,
  servicioId: null,
  configuracionJson: '',
})

export function FlujoPasosEditor({ flujoId, versionId, onClose }: { flujoId: string; versionId: string; onClose: () => void }) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [pasos, setPasos] = useState<PasoRow[]>([])

  const versionQuery = useQuery({
    queryKey: [`flujo-${flujoId}-version-${versionId}`],
    queryFn: () => flujosApi.getFlujoVersion(flujoId, versionId),
  })

  const agentesQuery = useQuery({ queryKey: ['agentes-all'], queryFn: () => flujosApi.listAgentes() })
  const serviciosQuery = useQuery({ queryKey: ['servicios-all'], queryFn: () => rpaApi.listServicios() })

  useEffect(() => {
    if (versionQuery.data) {
      setPasos(
        versionQuery.data.pasos
          .sort((a, b) => a.orden - b.orden)
          .map((p) => ({ key: p.id, ...p })),
      )
    }
  }, [versionQuery.data])

  const saveMutation = useMutation({
    mutationFn: () =>
      flujosApi.replacePasos(flujoId, versionId, {
        pasos: pasos.map((p, index) => ({
          orden: index + 1,
          nombre: p.nombre,
          tipoPaso: p.tipoPaso,
          agenteDefinicionId: p.tipoPaso === 'Agente' ? p.agenteDefinicionId || null : null,
          servicioId: p.tipoPaso === 'Rpa' ? p.servicioId || null : null,
          configuracionJson: p.configuracionJson || null,
        })),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [`flujo-${flujoId}-version-${versionId}`] })
      queryClient.invalidateQueries({ queryKey: [`flujo-${flujoId}-versiones`] })
      showToast('success', 'Pasos guardados.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudieron guardar los pasos.'),
  })

  const agentes = agentesQuery.data?.items ?? []
  const servicios = serviciosQuery.data?.items ?? []

  function updateRow(key: string, patch: Partial<PasoRow>) {
    setPasos((rows) => rows.map((r) => (r.key === key ? { ...r, ...patch } : r)))
  }

  function removeRow(key: string) {
    setPasos((rows) => rows.filter((r) => r.key !== key))
  }

  function moveRow(index: number, direction: -1 | 1) {
    setPasos((rows) => {
      const target = index + direction
      if (target < 0 || target >= rows.length) return rows
      const copy = [...rows]
      ;[copy[index], copy[target]] = [copy[target], copy[index]]
      return copy
    })
  }

  const version = versionQuery.data
  const esBorrador = version?.estado === 'Borrador'

  if (versionQuery.isLoading) return <p className="p-4 text-sm text-gray-500">Cargando…</p>

  return (
    <Card className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold text-gray-900">
          Pasos — v{version?.numeroVersion} {!esBorrador && <span className="text-sm font-normal text-gray-500">(solo lectura — {version?.estado})</span>}
        </h2>
        <Button variant="ghost" onClick={onClose}>
          Volver
        </Button>
      </div>

      <div className="flex flex-col gap-3">
        {pasos.map((paso, index) => (
          <div key={paso.key} className="rounded-lg border border-gray-200 p-3">
            <div className="flex items-start gap-3">
              <div className="flex flex-col gap-1 pt-1">
                <button
                  type="button"
                  className="text-gray-400 hover:text-gray-700 disabled:opacity-30"
                  disabled={!esBorrador || index === 0}
                  onClick={() => moveRow(index, -1)}
                  aria-label="Subir"
                >
                  ↑
                </button>
                <button
                  type="button"
                  className="text-gray-400 hover:text-gray-700 disabled:opacity-30"
                  disabled={!esBorrador || index === pasos.length - 1}
                  onClick={() => moveRow(index, 1)}
                  aria-label="Bajar"
                >
                  ↓
                </button>
              </div>

              <div className="flex flex-1 flex-col gap-2">
                <div className="flex items-center gap-2">
                  <span className="w-6 shrink-0 text-center text-xs font-medium text-gray-400">{index + 1}</span>
                  <input
                    className="flex-1 rounded-lg border border-gray-300 px-3 py-1.5 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100 disabled:bg-gray-50"
                    placeholder="Nombre del paso"
                    value={paso.nombre}
                    disabled={!esBorrador}
                    onChange={(e) => updateRow(paso.key, { nombre: e.target.value })}
                  />
                  <select
                    className="rounded-lg border border-gray-300 px-3 py-1.5 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100 disabled:bg-gray-50"
                    value={paso.tipoPaso}
                    disabled={!esBorrador}
                    onChange={(e) => updateRow(paso.key, { tipoPaso: e.target.value as TipoPaso })}
                  >
                    {tiposPaso.map((tipo) => (
                      <option key={tipo} value={tipo}>
                        {tipo}
                      </option>
                    ))}
                  </select>
                  <button
                    type="button"
                    className="text-gray-400 hover:text-red-600 disabled:opacity-30"
                    disabled={!esBorrador}
                    onClick={() => removeRow(paso.key)}
                    aria-label="Eliminar paso"
                  >
                    ✕
                  </button>
                </div>

                {paso.tipoPaso === 'Agente' && (
                  <select
                    className="rounded-lg border border-gray-300 px-3 py-1.5 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100 disabled:bg-gray-50"
                    value={paso.agenteDefinicionId ?? ''}
                    disabled={!esBorrador}
                    onChange={(e) => updateRow(paso.key, { agenteDefinicionId: e.target.value || null })}
                  >
                    <option value="">Seleccionar agente…</option>
                    {agentes.map((a) => (
                      <option key={a.id} value={a.id}>
                        {a.nombre}
                      </option>
                    ))}
                  </select>
                )}

                {paso.tipoPaso === 'Rpa' && (
                  <select
                    className="rounded-lg border border-gray-300 px-3 py-1.5 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100 disabled:bg-gray-50"
                    value={paso.servicioId ?? ''}
                    disabled={!esBorrador}
                    onChange={(e) => updateRow(paso.key, { servicioId: e.target.value || null })}
                  >
                    <option value="">Seleccionar servicio…</option>
                    {servicios.map((s) => (
                      <option key={s.id} value={s.id}>
                        {s.nombre}
                      </option>
                    ))}
                  </select>
                )}

                <textarea
                  className="rounded-lg border border-gray-300 px-3 py-1.5 font-mono text-xs focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100 disabled:bg-gray-50"
                  rows={2}
                  placeholder={configuracionHint[paso.tipoPaso]}
                  value={paso.configuracionJson ?? ''}
                  disabled={!esBorrador}
                  onChange={(e) => updateRow(paso.key, { configuracionJson: e.target.value })}
                />
              </div>
            </div>
          </div>
        ))}

        {pasos.length === 0 && <p className="text-sm text-gray-500">Sin pasos todavía.</p>}
      </div>

      {esBorrador && (
        <div className="flex items-center justify-between">
          <Button variant="ghost" onClick={() => setPasos((rows) => [...rows, newRow(rows.length + 1)])}>
            + Añadir paso
          </Button>
          <Button disabled={saveMutation.isPending} onClick={() => saveMutation.mutate()}>
            {saveMutation.isPending ? 'Guardando…' : 'Guardar pasos'}
          </Button>
        </div>
      )}
    </Card>
  )
}
