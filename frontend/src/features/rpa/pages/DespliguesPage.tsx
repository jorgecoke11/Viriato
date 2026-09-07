import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '../../../components/ui/Button'
import { Card } from '../../../components/ui/Card'
import { ConfirmDialog } from '../../../components/ui/ConfirmDialog'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/apiClient'
import { useToast } from '../../../lib/toast/useToast'
import * as flujosApi from '../../flujos/api'
import * as rpaApi from '../api'
import type { DespliegueConApiKeyDto, DespliegueDto } from '../api'

export function DespliguesPage() {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [showNuevo, setShowNuevo] = useState(false)
  const [equipoId, setEquipoId] = useState('')
  const [servicioId, setServicioId] = useState('')
  const [flujoId, setFlujoId] = useState('')
  const [revelado, setRevelado] = useState<DespliegueConApiKeyDto | null>(null)
  const [pendingDelete, setPendingDelete] = useState<DespliegueDto | null>(null)

  const despliguesQuery = useQuery({ queryKey: ['despliegues'], queryFn: () => rpaApi.listDespliegues() })
  const equiposQuery = useQuery({ queryKey: ['equipos-all'], queryFn: () => rpaApi.listEquipos() })
  const serviciosQuery = useQuery({ queryKey: ['servicios-all'], queryFn: () => rpaApi.listServicios() })
  const flujosQuery = useQuery({ queryKey: ['flujos-all'], queryFn: () => flujosApi.listFlujos() })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['despliegues'] })

  const createMutation = useMutation({
    mutationFn: () => rpaApi.createDespliegue({ equipoId, servicioId, flujoId }),
    onSuccess: (result) => {
      invalidate()
      setShowNuevo(false)
      setEquipoId('')
      setServicioId('')
      setFlujoId('')
      setRevelado(result)
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo crear el despliegue.'),
  })

  const toggleMutation = useMutation({
    mutationFn: ({ id, encendido }: { id: string; encendido: boolean }) => rpaApi.updateDespliegue(id, { encendido }),
    onSuccess: () => {
      invalidate()
      showToast('success', 'Despliegue actualizado.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo actualizar el despliegue.'),
  })

  const regenerarMutation = useMutation({
    mutationFn: (id: string) => rpaApi.regenerarClaveDespliegue(id),
    onSuccess: (result) => {
      invalidate()
      setRevelado(result)
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo regenerar la clave.'),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => rpaApi.deleteDespliegue(id),
    onSuccess: () => {
      invalidate()
      setPendingDelete(null)
      showToast('success', 'Despliegue eliminado.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo eliminar el despliegue.'),
  })

  const despliegues = despliguesQuery.data?.items ?? []
  const equipos = equiposQuery.data?.items.filter((e) => e.activo) ?? []
  const servicios = serviciosQuery.data?.items.filter((s) => s.activo) ?? []
  const flujos = flujosQuery.data?.items.filter((f) => f.activo) ?? []

  const nombreFlujo = (id: string) => flujosQuery.data?.items.find((f) => f.id === id)?.nombre ?? '—'

  async function copiarClave(clave: string) {
    try {
      await navigator.clipboard.writeText(clave)
      showToast('success', 'Clave copiada.')
    } catch {
      showToast('error', 'No se pudo copiar. Selecciónala manualmente.')
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold tracking-tight text-gray-900">Despliegues</h1>
        <Button onClick={() => setShowNuevo(true)}>Nuevo</Button>
      </div>

      <Card className="overflow-x-auto p-0">
        <table className="w-full min-w-[860px] text-left text-sm">
          <thead className="border-b border-gray-200 text-gray-500">
            <tr>
              <th className="px-4 py-3 font-medium">Equipo</th>
              <th className="px-4 py-3 font-medium">Servicio</th>
              <th className="px-4 py-3 font-medium">Proceso</th>
              <th className="px-4 py-3 font-medium">Encendido</th>
              <th className="px-4 py-3 font-medium">Clave</th>
              <th className="px-4 py-3 font-medium">Último uso</th>
              <th className="px-4 py-3 font-medium">Acciones</th>
            </tr>
          </thead>
          <tbody>
            {despliegues.map((despliegue) => (
              <tr key={despliegue.id} className="border-b border-gray-100 last:border-0">
                <td className="px-4 py-3">{despliegue.equipoNombre}</td>
                <td className="px-4 py-3">{despliegue.servicioNombre}</td>
                <td className="px-4 py-3">{nombreFlujo(despliegue.flujoId)}</td>
                <td className="px-4 py-3">
                  <label className="inline-flex items-center gap-2">
                    <input
                      type="checkbox"
                      className="accent-indigo-600"
                      checked={despliegue.encendido}
                      disabled={toggleMutation.isPending}
                      onChange={(e) => toggleMutation.mutate({ id: despliegue.id, encendido: e.target.checked })}
                    />
                    {despliegue.encendido ? 'Sí' : 'No'}
                  </label>
                </td>
                <td className="px-4 py-3 font-mono text-xs text-gray-500">{despliegue.apiKeyPrefix}…</td>
                <td className="px-4 py-3 text-gray-500">
                  {despliegue.lastUsedAt ? new Date(despliegue.lastUsedAt).toLocaleString() : 'Nunca'}
                </td>
                <td className="px-4 py-3">
                  <div className="flex items-center gap-3">
                    <button
                      type="button"
                      className="text-gray-500 hover:text-gray-900"
                      disabled={regenerarMutation.isPending}
                      onClick={() => regenerarMutation.mutate(despliegue.id)}
                    >
                      Regenerar clave
                    </button>
                    <button
                      type="button"
                      className="text-gray-500 hover:text-red-600"
                      onClick={() => setPendingDelete(despliegue)}
                    >
                      Eliminar
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {despliguesQuery.isLoading && <p className="p-4 text-sm text-gray-500">Cargando…</p>}
        {despliguesQuery.isSuccess && despliegues.length === 0 && <p className="p-4 text-sm text-gray-500">Sin despliegues.</p>}
      </Card>

      <Modal
        open={showNuevo}
        title="Nuevo despliegue"
        onClose={() => setShowNuevo(false)}
        footer={
          <div className="flex justify-end gap-2">
            <Button type="button" variant="ghost" onClick={() => setShowNuevo(false)}>
              Cancelar
            </Button>
            <Button
              type="button"
              disabled={!equipoId || !servicioId || !flujoId || createMutation.isPending}
              onClick={() => createMutation.mutate()}
            >
              {createMutation.isPending ? 'Creando…' : 'Crear'}
            </Button>
          </div>
        }
      >
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium text-gray-700">Equipo</label>
            <select
              className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
              value={equipoId}
              onChange={(e) => setEquipoId(e.target.value)}
            >
              <option value="">Seleccionar…</option>
              {equipos.map((equipo) => (
                <option key={equipo.id} value={equipo.id}>
                  {equipo.nombre}
                </option>
              ))}
            </select>
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium text-gray-700">Servicio</label>
            <select
              className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
              value={servicioId}
              onChange={(e) => setServicioId(e.target.value)}
            >
              <option value="">Seleccionar…</option>
              {servicios.map((servicio) => (
                <option key={servicio.id} value={servicio.id}>
                  {servicio.nombre}
                </option>
              ))}
            </select>
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium text-gray-700">Proceso</label>
            <select
              className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
              value={flujoId}
              onChange={(e) => setFlujoId(e.target.value)}
            >
              <option value="">Seleccionar…</option>
              {flujos.map((flujo) => (
                <option key={flujo.id} value={flujo.id}>
                  {flujo.nombre}
                </option>
              ))}
            </select>
          </div>
        </div>
      </Modal>

      <Modal
        open={revelado !== null}
        title="Clave de API"
        onClose={() => setRevelado(null)}
        footer={
          <div className="flex justify-end gap-2">
            {revelado && (
              <Button type="button" variant="ghost" onClick={() => copiarClave(revelado.apiKey)}>
                Copiar
              </Button>
            )}
            <Button type="button" onClick={() => setRevelado(null)}>
              Ya la he guardado
            </Button>
          </div>
        }
      >
        <p className="mb-3 text-sm text-gray-600">
          Copia esta clave ahora — no se puede volver a mostrar. Configúrala en el equipo donde corra este servicio.
        </p>
        <code className="block break-all rounded-lg bg-gray-100 p-3 text-xs text-gray-800">{revelado?.apiKey}</code>
      </Modal>

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Eliminar despliegue"
        message="¿Seguro que quieres eliminar este despliegue? El robot dejará de poder autenticarse de inmediato."
        pending={deleteMutation.isPending}
        onConfirm={() => pendingDelete && deleteMutation.mutate(pendingDelete.id)}
        onCancel={() => setPendingDelete(null)}
      />
    </div>
  )
}
