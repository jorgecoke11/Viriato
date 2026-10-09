import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, RefreshCw, Server, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Button } from '../../../components/ui/Button'
import { Card } from '../../../components/ui/Card'
import { IconButton } from '../../../components/ui/IconButton'
import { EmptyState } from '../../../components/ui/EmptyState'
import { PageHeader } from '../../../components/ui/PageHeader'
import { Skeleton } from '../../../components/ui/Skeleton'
import { Switch } from '../../../components/ui/Switch'
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
  const [flujoDestinoId, setFlujoDestinoId] = useState('')
  const [revelado, setRevelado] = useState<DespliegueConApiKeyDto | null>(null)
  const [pendingDelete, setPendingDelete] = useState<DespliegueDto | null>(null)

  const despliguesQuery = useQuery({ queryKey: ['despliegues'], queryFn: () => rpaApi.listDespliegues() })
  const equiposQuery = useQuery({ queryKey: ['equipos-all'], queryFn: () => rpaApi.listEquipos() })
  const serviciosQuery = useQuery({ queryKey: ['servicios-all'], queryFn: () => rpaApi.listServicios() })
  const flujosQuery = useQuery({ queryKey: ['flujos-all'], queryFn: () => flujosApi.listFlujos() })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['despliegues'] })

  const createMutation = useMutation({
    mutationFn: () => rpaApi.createDespliegue({ equipoId, servicioId, flujoId, ...(flujoDestinoId ? { flujoDestinoId } : {}) }),
    onSuccess: (result) => {
      invalidate()
      setShowNuevo(false)
      setEquipoId('')
      setServicioId('')
      setFlujoId('')
      setFlujoDestinoId('')
      setRevelado(result)
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo crear el despliegue.'),
  })

  const toggleMutation = useMutation({
    mutationFn: ({ id, ...cambios }: { id: string } & rpaApi.UpdateDespliegueInput) => rpaApi.updateDespliegue(id, cambios),
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
      <PageHeader
        title="Despliegues"
        description="Un servicio instalado en un equipo para un proceso: de aquí sale la clave con la que se identifica el robot."
        actions={
          <Button onClick={() => setShowNuevo(true)}>
            <Plus size={16} />
            Nuevo
          </Button>
        }
      />

      <Card className="overflow-x-auto p-0">
        <table className="w-full min-w-[860px] text-left text-sm">
          <thead className="border-b border-gray-200 bg-gray-50/70">
            <tr>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">Equipo</th>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">Servicio</th>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">Proceso</th>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">Puede crear casos en</th>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">Encendido</th>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">Clave</th>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">Último uso</th>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">Acciones</th>
            </tr>
          </thead>
          <tbody>
            {despliegues.map((despliegue) => (
              <tr key={despliegue.id} className="border-b border-gray-100 last:border-0 hover:bg-gray-50/70">
                <td className="px-4 py-3">{despliegue.equipoNombre}</td>
                <td className="px-4 py-3">{despliegue.servicioNombre}</td>
                <td className="px-4 py-3">{nombreFlujo(despliegue.flujoId)}</td>
                <td className="px-4 py-3">
                  <select
                    aria-label={`Proceso en el que ${despliegue.servicioNombre} puede crear casos`}
                    className="max-w-[200px] field !min-h-8 !w-auto !px-2 !py-1"
                    value={despliegue.flujoDestinoId ?? ''}
                    disabled={toggleMutation.isPending}
                    onChange={(e) =>
                      toggleMutation.mutate(
                        e.target.value
                          ? { id: despliegue.id, flujoDestinoId: e.target.value }
                          : { id: despliegue.id, quitarFlujoDestino: true },
                      )
                    }
                  >
                    <option value="">Ninguno</option>
                    {flujos.map((flujo) => (
                      <option key={flujo.id} value={flujo.id}>
                        {flujo.nombre}
                      </option>
                    ))}
                  </select>
                </td>
                <td className="px-4 py-3">
                  <span className="inline-flex items-center gap-2.5">
                    <Switch
                      checked={despliegue.encendido}
                      label={`${despliegue.encendido ? 'Apagar' : 'Encender'} el despliegue de ${despliegue.servicioNombre}`}
                      disabled={toggleMutation.isPending}
                      onChange={(encendido) => toggleMutation.mutate({ id: despliegue.id, encendido })}
                    />
                    <span className={despliegue.encendido ? 'text-green-700' : 'text-gray-500'}>{despliegue.encendido ? 'Sí' : 'No'}</span>
                  </span>
                </td>
                <td className="px-4 py-3 font-mono text-xs text-gray-500">{despliegue.apiKeyPrefix}…</td>
                <td className="px-4 py-3 text-gray-500">
                  {despliegue.lastUsedAt ? new Date(despliegue.lastUsedAt).toLocaleString() : 'Nunca'}
                </td>
                <td className="px-4 py-3">
                  <div className="flex items-center gap-0.5">
                    <IconButton
                      size="sm"
                      label="Regenerar clave"
                      disabled={regenerarMutation.isPending}
                      onClick={() => regenerarMutation.mutate(despliegue.id)}
                    >
                      <RefreshCw size={16} />
                    </IconButton>
                    <IconButton size="sm" variant="danger" label="Eliminar despliegue" onClick={() => setPendingDelete(despliegue)}>
                      <Trash2 size={16} />
                    </IconButton>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {despliguesQuery.isLoading && (
          <div role="status" aria-label="Cargando" className="flex flex-col gap-3 p-4">
            <Skeleton className="h-5 w-11/12" />
            <Skeleton className="h-5 w-4/5" />
            <Skeleton className="h-5 w-3/5" />
          </div>
        )}
        {despliguesQuery.isSuccess && despliegues.length === 0 && (
          <EmptyState
            icon={<Server size={22} />}
            title="Sin despliegues"
            description="Crea uno para que un robot pueda identificarse y empezar a trabajar."
            action={
              <Button variant="secondary" size="sm" onClick={() => setShowNuevo(true)}>
                <Plus size={15} />
                Nuevo
              </Button>
            }
          />
        )}
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
              className="field"
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
              className="field"
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
              className="field"
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
          <div className="flex flex-col gap-1">
            <label className="text-sm font-medium text-gray-700">Puede crear casos en (opcional)</label>
            <select
              className="field"
              value={flujoDestinoId}
              onChange={(e) => setFlujoDestinoId(e.target.value)}
            >
              <option value="">Ninguno</option>
              {flujos.map((flujo) => (
                <option key={flujo.id} value={flujo.id}>
                  {flujo.nombre}
                </option>
              ))}
            </select>
            <p className="text-xs text-gray-500">
              Solo para robots que lanzan casos de otro proceso. Sin esto, el robot no puede crear ninguno.
            </p>
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
