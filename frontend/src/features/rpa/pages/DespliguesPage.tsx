import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Cpu, Plus, Server, Settings2, Trash2, Video } from 'lucide-react'
import { useState } from 'react'
import { Badge } from '../../../components/ui/Badge'
import { Button } from '../../../components/ui/Button'
import type { ColumnaDeTabla } from '../../../components/ui/DataTable'
import { IconButton } from '../../../components/ui/IconButton'
import { ListaDeDatos } from '../../../components/ui/ListaDeDatos'
import { PageHeader } from '../../../components/ui/PageHeader'
import { Switch } from '../../../components/ui/Switch'
import { ConfirmDialog } from '../../../components/ui/ConfirmDialog'
import { Modal } from '../../../components/ui/Modal'
import { mensajeDeError } from '../../../lib/apiClient'
import { useToast } from '../../../lib/toast/useToast'
import { useListaPaginada } from '../../../lib/useListaPaginada'
import * as flujosApi from '../../flujos/api'
import * as rpaApi from '../api'
import type { DespliegueConApiKeyDto, DespliegueDto } from '../api'
import { ConfiguracionDelDespliegueModal, type ApartadoDelDespliegue } from '../ConfiguracionDelDespliegueModal'
import { AjustesDelLanzadorModal } from '../AjustesDelLanzadorModal'
import { calidadDeVista, etiquetaDeGrabacion } from '../pantallaDelRobot'

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
  const [configurandoId, setConfigurandoId] = useState<string | null>(null)
  const [apartado, setApartado] = useState<ApartadoDelDespliegue>('general')
  const [verLanzador, setVerLanzador] = useState(false)
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
    onError: (err) => showToast('error', mensajeDeError(err, 'No se pudo crear el despliegue.')),
  })

  const toggleMutation = useMutation({
    mutationFn: ({ id, ...cambios }: { id: string } & rpaApi.UpdateDespliegueInput) => rpaApi.updateDespliegue(id, cambios),
    onSuccess: () => {
      invalidate()
      showToast('success', 'Despliegue actualizado.')
    },
    onError: (err) => showToast('error', mensajeDeError(err, 'No se pudo actualizar el despliegue.')),
  })

  const regenerarMutation = useMutation({
    mutationFn: (id: string) => rpaApi.regenerarClaveDespliegue(id),
    onSuccess: (result) => {
      invalidate()
      setRevelado(result)
    },
    onError: (err) => showToast('error', mensajeDeError(err, 'No se pudo regenerar la clave.')),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => rpaApi.deleteDespliegue(id),
    onSuccess: () => {
      invalidate()
      setPendingDelete(null)
      showToast('success', 'Despliegue eliminado.')
    },
    onError: (err) => showToast('error', mensajeDeError(err, 'No se pudo eliminar el despliegue.')),
  })

  const equipos = equiposQuery.data?.items.filter((e) => e.activo) ?? []
  const servicios = serviciosQuery.data?.items.filter((s) => s.activo) ?? []
  const flujos = flujosQuery.data?.items.filter((f) => f.activo) ?? []

  const nombreFlujo = (id: string) => flujosQuery.data?.items.find((f) => f.id === id)?.nombre ?? '—'
  // The live row, so the modal shows what was just saved.
  const configurando = fuente.filas.find((d) => d.id === configurandoId) ?? null

  function configurar(d: DespliegueDto, en: ApartadoDelDespliegue = 'general') {
    setApartado(en)
    setConfigurandoId(d.id)
  }

  async function copiarClave(clave: string) {
    try {
      await navigator.clipboard.writeText(clave)
      showToast('success', 'Clave copiada.')
    } catch {
      showToast('error', 'No se pudo copiar. Selecciónala manualmente.')
    }
  }

  const columnas: ColumnaDeTabla<DespliegueDto>[] = [
    {
      clave: 'despliegue',
      titulo: 'Despliegue',
      celda: (d) => (
        <div className="flex flex-col gap-0.5">
          <span className="font-medium text-gray-900">{d.servicioNombre}</span>
          <span className="text-xs text-gray-500">
            {d.equipoNombre} · {nombreFlujo(d.flujoId)}
          </span>
        </div>
      ),
    },
    {
      clave: 'encendido',
      titulo: 'Encendido',
      celda: (d) => (
        <span className="inline-flex items-center gap-2.5" onClick={(e) => e.stopPropagation()}>
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
    {
      clave: 'robot',
      titulo: 'Robot',
      celda: (d) =>
        !d.enElLanzador ? (
          <span className="text-gray-400" title="No lo arranca el lanzador de Viriato: corre en otro sitio, con su clave">
            Fuera
          </span>
        ) : d.versionDeRobotNumero === null ? (
          <span title="Lo arrancará el lanzador en cuanto subas una versión en Servicios">
            <Badge tone="warning">Sin versión</Badge>
          </span>
        ) : (
          <span className="inline-flex items-center gap-1.5">
            <Badge tone={d.versionFijadaId ? 'neutral' : 'brand'}>
              {d.versionFijadaId ? `Fijada en v${d.versionDeRobotNumero}` : `v${d.versionDeRobotNumero}`}
            </Badge>
            <span className="text-xs text-gray-500">{d.replicas === 0 ? 'aparcado' : `× ${d.replicas}`}</span>
          </span>
        ),
    },
    {
      clave: 'pantalla',
      titulo: 'Vista en directo',
      celda: (d) => {
        const calidad = calidadDeVista(d.vistaEnDirecto)
        return (
          <span className="inline-flex items-center gap-1.5">
            <Badge tone={calidad.tono}>{calidad.titulo}</Badge>
            {d.grabacionDeVideo !== 'SegunRobot' && (
              <span title={etiquetaDeGrabacion(d.grabacionDeVideo)} className={d.grabacionDeVideo === 'Activa' ? 'text-gray-600' : 'text-gray-300'}>
                <Video size={15} aria-label={etiquetaDeGrabacion(d.grabacionDeVideo)} />
              </span>
            )}
          </span>
        )
      },
    },
    {
      clave: 'uso',
      titulo: 'Último uso',
      celda: (d) => <span className="text-gray-500">{d.lastUsedAt ? new Date(d.lastUsedAt).toLocaleString() : 'Nunca'}</span>,
    },
    {
      clave: 'acciones',
      titulo: '',
      celda: (d) => (
        <div className="-my-1 flex items-center justify-end gap-0.5" onClick={(e) => e.stopPropagation()}>
          <IconButton size="sm" label="Configurar" onClick={() => configurar(d)}>
            <Settings2 size={16} />
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
          <div className="flex items-center gap-2">
            <Button variant="secondary" onClick={() => setVerLanzador(true)}>
              <Cpu size={16} />
              Lanzador
            </Button>
            <Button onClick={() => setShowNuevo(true)}>
              <Plus size={16} />
              Nuevo
            </Button>
          </div>
        }
      />

      <ListaDeDatos
        fuente={fuente}
        columnas={columnas}
        obtenerId={(d) => d.id}
        nombreDeFila={(d) => `${d.servicioNombre} en ${d.equipoNombre}`}
        entidad={{ singular: 'despliegue', plural: 'despliegues' }}
        buscador={false}
        alHacerClic={(d) => configurar(d)}
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
        anchoMinimo="1160px"
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
          Copia esta clave ahora — no se puede volver a mostrar. Solo hace falta para un robot que corre en otra máquina: si lo
          arranca el lanzador de Viriato (subiendo su versión), no se usa.
        </p>
        <code className="block break-all rounded-lg bg-gray-100 p-3 text-xs text-gray-800">{revelado?.apiKey}</code>
      </Modal>

      <ConfiguracionDelDespliegueModal
        despliegue={configurando}
        apartado={apartado}
        alCambiarApartado={setApartado}
        flujos={flujos}
        nombreFlujo={nombreFlujo}
        regenerando={regenerarMutation.isPending}
        alRegenerarClave={(d) => regenerarMutation.mutate(d.id)}
        onClose={() => setConfigurandoId(null)}
      />
      <AjustesDelLanzadorModal open={verLanzador} onClose={() => setVerLanzador(false)} />

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
