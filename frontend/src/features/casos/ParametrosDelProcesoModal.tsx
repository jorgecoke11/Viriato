import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { Button } from '../../components/ui/Button'
import { Modal } from '../../components/ui/Modal'
import { ApiError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import * as flujosApi from '../flujos/api'

/** A short single-line value gets an input; anything long or on several lines (a list of products, a JSON) gets room to breathe. */
const esLargo = (valor: string) => valor.includes('\n') || valor.length > 60

/**
 * The parameters of a process that the people working it may change: the ones the process's author opened up, each under its
 * own name and with its explanation. Saved together. The robots read the parameters when they start a case, so a change counts
 * from the next case they take — it does not touch the ones already running.
 */
export function ParametrosDelProcesoModal({
  abierto,
  flujoId,
  flujoNombre,
  alCerrar,
}: {
  abierto: boolean
  flujoId: string
  flujoNombre: string
  alCerrar: () => void
}) {
  const { showToast } = useToast()
  const queryClient = useQueryClient()

  const query = useQuery({
    queryKey: ['parametros-editables', flujoId],
    queryFn: () => flujosApi.listParametrosEditables(flujoId),
    enabled: abierto,
  })
  const parametros = query.data ?? []

  const [valores, setValores] = useState<Record<string, string>>({})
  // What is in the boxes starts from what the server has each time the window opens (or the server's values change).
  useEffect(() => {
    if (abierto) setValores(Object.fromEntries((query.data ?? []).map((p) => [p.id, p.valor])))
  }, [abierto, query.data])

  const cambiados = parametros.filter((p) => valores[p.id] !== undefined && valores[p.id] !== p.valor)

  const guardar = useMutation({
    mutationFn: () => flujosApi.guardarParametrosEditables(flujoId, cambiados.map((p) => ({ id: p.id, valor: valores[p.id] }))),
    onSuccess: (guardados) => {
      queryClient.setQueryData(['parametros-editables', flujoId], guardados)
      showToast('success', cambiados.length === 1 ? 'Parámetro guardado.' : 'Parámetros guardados.')
      alCerrar()
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudieron guardar los parámetros.'),
  })

  return (
    <Modal
      open={abierto}
      title={`Parámetros — ${flujoNombre}`}
      onClose={alCerrar}
      footer={
        <div className="flex items-center justify-between gap-3">
          <span className="text-xs text-gray-500">{cambiados.length > 0 ? `${cambiados.length} sin guardar` : ''}</span>
          <div className="flex gap-2">
            <Button type="button" variant="ghost" onClick={alCerrar}>
              Cancelar
            </Button>
            <Button type="button" disabled={cambiados.length === 0 || guardar.isPending} onClick={() => guardar.mutate()}>
              {guardar.isPending ? 'Guardando…' : 'Guardar'}
            </Button>
          </div>
        </div>
      }
    >
      {query.isLoading ? (
        <p className="text-gray-500">Cargando…</p>
      ) : query.isError ? (
        <p className="text-sm text-red-600">No se pudieron cargar los parámetros.</p>
      ) : parametros.length === 0 ? (
        <p className="text-sm text-gray-600">Este proceso no tiene parámetros que puedas cambiar desde aquí.</p>
      ) : (
        <div className="flex flex-col gap-4">
          <p className="text-sm text-gray-500">
            Los robots leen estos valores al empezar cada caso: lo que cambies vale desde el siguiente que tomen.
          </p>
          {parametros.map((p) => {
            const valor = valores[p.id] ?? p.valor
            const id = `parametro-${p.id}`
            return (
              <div key={p.id} className="flex flex-col gap-1.5">
                <label htmlFor={id} className="text-sm font-medium text-gray-700">
                  {p.etiqueta}
                  {valor !== p.valor && <span className="ml-2 text-xs font-normal text-amber-600">modificado</span>}
                </label>
                {esLargo(p.valor) || esLargo(valor) ? (
                  <textarea id={id} rows={5} className="field font-mono text-xs" value={valor} onChange={(e) => setValores({ ...valores, [p.id]: e.target.value })} />
                ) : (
                  <input id={id} className="field" value={valor} onChange={(e) => setValores({ ...valores, [p.id]: e.target.value })} />
                )}
                {p.descripcion && <p className="text-xs text-gray-500">{p.descripcion}</p>}
              </div>
            )
          })}
        </div>
      )}
    </Modal>
  )
}
