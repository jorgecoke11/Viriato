import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Plus, RefreshCw, Server, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Button } from '../../../components/ui/Button'
import type { ColumnaDeTabla } from '../../../components/ui/DataTable'
import { IconButton } from '../../../components/ui/IconButton'
import { ListaDeDatos } from '../../../components/ui/ListaDeDatos'
import { PageHeader } from '../../../components/ui/PageHeader'
import { Switch } from '../../../components/ui/Switch'
import { ConfirmDialog } from '../../../components/ui/ConfirmDialog'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/apiClient'
import { useToast } from '../../../lib/toast/useToast'
import { useListaPaginada } from '../../../lib/useListaPaginada'
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

  const fuente = useListaPaginada<DespliegueDto>({
    clave: ['despliegues'],
    obtenerId: (d) => d.id,
    cargar: ({ pagina, tamano }) => rpaApi.listDespliegues(pagina, tamano),
  })
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

  const columnas: ColumnaDeTabla<DespliegueDto>[] = [
    { clave: 'equipo', titulo: 'Equipo', celda: (d) => d.equipoNombre },
    { clave: 'servicio', titulo: 'Servicio', celda: (d) => d.servicioNombre },
    { clave: 'proceso', titulo: 'Proceso', celda: (d) => nombreFlujo(d.flujoId) },
    {
      clave: 'destino',
      titulo: 'Puede crear casos en',
      celda: (d) => (
        <select
          aria-label={`Proceso en el que ${d.servicioNombre} puede crear casos`}
          className="max-w-[200px] field !min-h-8 !w-auto !px-2 !py-1"
          value={d.flujoDestinoId ?? ''}
          disabled={toggleMutation.isPending}
          onChange={(e) =>
            toggleMutation.mutate(e.target.value ? { id: d.id, flujoDestinoId: e.target.value } : { id: d.id, quitarFlujoDestino: true })
          }
        >
          <option value="">Ninguno</option>
          {flujos.map((flujo) => (
            <option key={flujo.id} value={flujo.id}>
              {flujo.nombre}
            </option>
          ))}
        </select>
      ),
    },
    {
      clave: 'encendido',
      titulo: 'Encendido',
      celda: (d) => (
        <span className="inline-flex items-center gap-2.5">
          <Switch
            checked={d.encendido}
            label={`${d.encendido ? 'Apagar' : 'Encender'} el despliegue de ${d.servicioNombre}`}
            disabled={toggleMutation.isPending}
            onChange={(encendido) => toggleMutation.mutate({ id: d.id, encendido })}
          />
          <span className={d.encendido ? 'text-green-700' : 'text-gray-500'}>{d.encendido ? 'Sí' : 'No'}</span>
        </span>
      ),
    },
    { clave: 'clave', titulo: 'Clave', celda: (d) => <span className="font-mono text-xs text-gray-500">{d.apiKeyPrefix}…</span> },
    {
      clave: 'uso',
      titulo: 'Último uso',
      celda: (d) => <span className="text-gray-500">{d.lastUsedAt ? new Date(d.lastUsedAt).toLocaleString() : 'Nunca'}</span>,
    },
    {
      clave: 'acciones',
      titulo: 'Acciones',
      celda: (d) => (
        <div className="-my-1 flex items-center gap-0.5">
          <IconButton size="sm" label="Regenerar clave" disabled={regenerarMutation.isPending} onClick={() => regenerarMutation.mutate(d.id)}>
            <RefreshCw size={16} />
          </IconButton>
          <IconButton size="sm" variant="danger" label="Eliminar despliegue" onClick={() => setPendingDelete(d)}>
            <Trash2 size={16} />
          </IconButton>
        </div>
      ),
    },
  ]

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

      <ListaDeDatos
        fuente={fuente}
        columnas={columnas}
        obtenerId={(d) => d.id}
        nombreDeFila={(d) => `${d.servicioNombre} en ${d.equipoNombre}`}
        entidad={{ singular: 'despliegue', plural: 'despliegues' }}
        buscador={false}
        vacio={{
          icono: <Server size={22} />,
          titulo: 'Sin despliegues',
          descripcion: 'Crea uno para que un robot pueda identificarse y empezar a trabajar.',
          accion: (
            <Button variant="secondary" size="sm" onClick={() => setShowNuevo(true)}>
              <Plus size={15} />
              Nuevo
            </Button>
          ),
        }}
        anchoMinimo="860px"
      />

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
