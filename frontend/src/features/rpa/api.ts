import { apiFetch } from '../../lib/apiClient'
import type { PagedResult } from '../../lib/types'
import type { CalidadVistaEnDirecto, GrabacionDeVideo } from './pantallaDelRobot'

export interface EquipoDto {
  id: string
  nombre: string
  descripcion: string | null
  activo: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateEquipoInput {
  nombre: string
  descripcion?: string | null
}

export interface UpdateEquipoInput {
  nombre?: string
  descripcion?: string | null
  activo?: boolean
}

export interface ServicioDto {
  id: string
  nombre: string
  descripcion: string | null
  activo: boolean
  /** How many steps of the service may run at once across all machines; null = no limit. */
  maxEjecucionesGlobales: number | null
  /** The longest a step of the service may stay in execution, in minutes; null = no limit. Past it the caso is cancelled. */
  tiempoMaximoMinutos: number | null
  createdAt: string
  updatedAt: string
  /** Number of the latest version of its robot uploaded; null with none. */
  ultimaVersion: number | null
}

export interface CreateServicioInput {
  nombre: string
  descripcion?: string | null
  maxEjecucionesGlobales?: number | null
  tiempoMaximoMinutos?: number | null
}

export interface UpdateServicioInput {
  nombre?: string
  descripcion?: string | null
  activo?: boolean
  maxEjecucionesGlobales?: number | null
  quitarLimiteGlobal?: boolean
  tiempoMaximoMinutos?: number | null
  quitarTiempoMaximo?: boolean
}

export interface DespliegueDto {
  id: string
  equipoId: string
  equipoNombre: string
  servicioId: string
  servicioNombre: string
  flujoId: string
  /** The one process this robot may create Casos in; null when it may not create any. */
  flujoDestinoId: string | null
  encendido: boolean
  apiKeyPrefix: string
  createdAt: string
  updatedAt: string
  lastUsedAt: string | null
  /** Whether its robot's screen can be watched live, and how smoothly: the platform decides, not the robot. */
  vistaEnDirecto: CalidadVistaEnDirecto
  /** Whether its robot records each run as a video. */
  grabacionDeVideo: GrabacionDeVideo
  /** The version of its service's robot the launcher next to Viriato runs; null when it runs somewhere else, with its key, or
   * its service has none uploaded yet. */
  versionDeRobotNumero: number | null
  /** The launcher next to Viriato runs it (with its service's robot). */
  enElLanzador: boolean
  /** The version it is held at; null follows the latest. */
  versionFijadaId: string | null
  /** Replicas: copies of its robot the launcher may run at once when it has work (0 = parked). Set here, not by the robot. */
  replicas: number
  /** Memory ceiling of a copy, in MB; null = what its version says. */
  memoriaMaximaMb: number | null
}

/** How much the launcher next to Viriato may run at once on its machine. */
export interface AjustesDelLanzadorDto {
  maxRobotsALaVez: number
  maxConNavegador: number
}

/** One upload of a service's robot (the zip of its `dotnet publish`). */
export interface VersionDeRobotDto {
  id: string
  numero: number
  nombreArchivo: string
  tamanoBytes: number
  /** How it is run, worked out from the zip, e.g. `dotnet Bsh.PricesExtractor.dll`. */
  ejecuta: string
  /** It drives a browser. */
  pantalla: boolean
  memoriaMaximaMb: number | null
  subidaPor: string | null
  createdAt: string
  /** The latest: what every despliegue not held at another one runs. */
  ultima: boolean
  /** The machines whose despliegue the launcher runs with it. */
  usadaPor: string[]
}

export interface DespliegueConApiKeyDto {
  despliegue: DespliegueDto
  apiKey: string
}

export interface CreateDespliegueInput {
  equipoId: string
  servicioId: string
  flujoId: string
  flujoDestinoId?: string
}

export interface UpdateDespliegueInput {
  encendido?: boolean
  flujoDestinoId?: string
  /** True removes the permission to create Casos. */
  quitarFlujoDestino?: boolean
  vistaEnDirecto?: CalidadVistaEnDirecto
  grabacionDeVideo?: GrabacionDeVideo
}

/** Never carries the password: it is write-only from the web, and only reaches a robot. */
export interface CredencialDto {
  id: string
  nombre: string
  descripcion: string | null
  usuario: string | null
  servicioId: string | null
  servicioNombre: string | null
  activo: boolean
  ultimoAccesoAt: string | null
  createdAt: string
  updatedAt: string
}

export interface CreateCredencialInput {
  nombre: string
  descripcion?: string | null
  usuario?: string | null
  password: string
  servicioId?: string | null
}

/** A full replacement of what is editable: null servicioId = any robot, empty password = keep the stored one. */
export interface UpdateCredencialInput {
  descripcion: string | null
  usuario: string | null
  servicioId: string | null
  activo: boolean
  password?: string | null
}

const buildQuery = (params: Record<string, string | undefined>) => {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value) search.set(key, value)
  }
  const query = search.toString()
  return query ? `?${query}` : ''
}

export const listEquipos = (filters: Record<string, string> = {}) =>
  apiFetch<PagedResult<EquipoDto>>(`/equipos${buildQuery({ searchTerm: filters.search, page: filters.page, pageSize: filters.pageSize ?? '100' })}`)

export const createEquipo = (input: CreateEquipoInput) =>
  apiFetch<EquipoDto>('/equipos', { method: 'POST', body: JSON.stringify(input) })

export const updateEquipo = (id: string, input: UpdateEquipoInput) =>
  apiFetch<EquipoDto>(`/equipos/${id}`, { method: 'PATCH', body: JSON.stringify(input) })

export const deleteEquipo = (id: string) => apiFetch<void>(`/equipos/${id}`, { method: 'DELETE' })

export const listServicios = (filters: Record<string, string> = {}) =>
  apiFetch<PagedResult<ServicioDto>>(`/servicios${buildQuery({ searchTerm: filters.search, page: filters.page, pageSize: filters.pageSize ?? '100' })}`)

export const createServicio = (input: CreateServicioInput) =>
  apiFetch<ServicioDto>('/servicios', { method: 'POST', body: JSON.stringify(input) })

export const updateServicio = (id: string, input: UpdateServicioInput) =>
  apiFetch<ServicioDto>(`/servicios/${id}`, { method: 'PATCH', body: JSON.stringify(input) })

export const deleteServicio = (id: string) => apiFetch<void>(`/servicios/${id}`, { method: 'DELETE' })

export const listDespliegues = (page = 1, pageSize = 100) =>
  apiFetch<PagedResult<DespliegueDto>>(`/despliegues${buildQuery({ page: String(page), pageSize: String(pageSize) })}`)

export const createDespliegue = (input: CreateDespliegueInput) =>
  apiFetch<DespliegueConApiKeyDto>('/despliegues', { method: 'POST', body: JSON.stringify(input) })

export const updateDespliegue = (id: string, input: UpdateDespliegueInput) =>
  apiFetch<DespliegueDto>(`/despliegues/${id}`, { method: 'PATCH', body: JSON.stringify(input) })

export const regenerarClaveDespliegue = (id: string) =>
  apiFetch<DespliegueConApiKeyDto>(`/despliegues/${id}/regenerar-clave`, { method: 'POST' })

export const deleteDespliegue = (id: string) => apiFetch<void>(`/despliegues/${id}`, { method: 'DELETE' })

export const listVersionesDeRobot = (servicioId: string) => apiFetch<VersionDeRobotDto[]>(`/servicios/${servicioId}/versiones`)

export const subirVersionDeRobot = (servicioId: string, file: File) => {
  const formData = new FormData()
  formData.append('file', file)
  return apiFetch<VersionDeRobotDto>(`/servicios/${servicioId}/versiones`, { method: 'POST', body: formData })
}

export const borrarVersionDeRobot = (servicioId: string, versionId: string) =>
  apiFetch<void>(`/servicios/${servicioId}/versiones/${versionId}`, { method: 'DELETE' })

/** Whether the launcher runs this despliegue, and with which version of its service's robot (`versionId` null = the latest). */
export const guardarRobotDelDespliegue = (despliegueId: string, input: { enElLanzador: boolean; versionId: string | null }) =>
  apiFetch<DespliegueDto>(`/despliegues/${despliegueId}/robot`, { method: 'PUT', body: JSON.stringify(input) })

/** `memoriaMaximaMb` null: what the robot's version says. */
export const guardarRobotEnElLanzador = (despliegueId: string, input: { replicas: number; memoriaMaximaMb: number | null }) =>
  apiFetch<DespliegueDto>(`/despliegues/${despliegueId}/lanzador`, { method: 'PUT', body: JSON.stringify(input) })

export const getAjustesDelLanzador = () => apiFetch<AjustesDelLanzadorDto>('/lanzador/ajustes')

export const guardarAjustesDelLanzador = (input: AjustesDelLanzadorDto) =>
  apiFetch<AjustesDelLanzadorDto>('/lanzador/ajustes', { method: 'PUT', body: JSON.stringify(input) })

export const listCredenciales = (filters: Record<string, string> = {}) =>
  apiFetch<PagedResult<CredencialDto>>(`/credenciales${buildQuery({ searchTerm: filters.search, page: filters.page, pageSize: filters.pageSize ?? '100' })}`)

export const createCredencial = (input: CreateCredencialInput) =>
  apiFetch<CredencialDto>('/credenciales', { method: 'POST', body: JSON.stringify(input) })

export const updateCredencial = (id: string, input: UpdateCredencialInput) =>
  apiFetch<CredencialDto>(`/credenciales/${id}`, { method: 'PUT', body: JSON.stringify(input) })

export const deleteCredencial = (id: string) => apiFetch<void>(`/credenciales/${id}`, { method: 'DELETE' })

// ---------------------------------------------------------------- dispatch: which service of a machine goes first

export type PoliticaDespacho = 'Prioridad' | 'Turnos'

export interface ServicioOrdenDto {
  servicioId: string
  servicioNombre: string
}

/** A service the machine runs (it has a Despliegue there). */
export interface ServicioDesplegadoDto extends ServicioOrdenDto {
  despliegues: number
}

export interface DespachoEquipoDto {
  equipoId: string
  equipoNombre: string
  /** The optional ceiling of steps at once; null = none, the robot copies the stack runs set the capacity. */
  maxEjecucionesSimultaneas: number | null
  politica: PoliticaDespacho
  orden: ServicioOrdenDto[]
  serviciosDelEquipo: ServicioDesplegadoDto[]
}

export interface UpdateDespachoInput {
  maxEjecucionesSimultaneas: number | null
  politica: PoliticaDespacho
  /** Service ids, first to last. */
  orden: string[]
}

export interface PlantillaDespachoDto {
  id: string
  nombre: string
  descripcion: string | null
  maxEjecucionesSimultaneas: number | null
  politica: PoliticaDespacho
  orden: ServicioOrdenDto[]
  createdAt: string
  updatedAt: string
}

export interface SavePlantillaDespachoInput extends UpdateDespachoInput {
  nombre: string
  descripcion: string | null
}

export interface EnEjecucionDto {
  casoId: string
  casoTitulo: string
  servicioId: string
  servicioNombre: string
  desde: string
  /** The step being run, to cancel it. */
  ejecucionPasoId: string
  tiempoMaximoMinutos: number | null
  limiteAt: string | null
  /** Past its time: it no longer counts as running and its caso is about to be cancelled. */
  vencido: boolean
}

export interface PendienteDto {
  posicion: number
  casoId: string
  casoTitulo: string
  servicioId: string
  servicioNombre: string
  esperaDesde: string
  robotEncendido: boolean
  robotConectado: boolean
  robotOcupado: boolean
  /** The service already has as many steps running as its global cap allows. */
  servicioAlLimiteGlobal: boolean
  /** The priority of the execution: higher goes first among the ones waiting for the same service. */
  prioridad: number
  /** The step this execution is, to change its priority or cancel it. */
  ejecucionPasoId: string
}

/** A robot of the machine and how many running copies of it there are (replicas, or processes started with its key). */
export interface RobotDelEquipoDto {
  despliegueId: string
  servicioId: string
  servicioNombre: string
  encendido: boolean
  conectado: boolean
  /** Copies asking for work or holding a step. */
  instancias: number
  /** Of those, the ones that hold nothing and could take a step. */
  libres: number
  /** What the launcher that starts this robot on demand last said; null when nothing launches it (it is just left running). */
  lanzador?: LanzadorDelRobotDto | null
}

/** A launcher's word on one robot: it starts it only while it has work. */
export interface LanzadorDelRobotDto {
  /** SinTrabajo, EnMarcha, EnEspera, Apagado or Fallo. */
  estado: string
  copias: number
  replicas: number
  /** Why: what it waits for, or what failed. */
  detalle: string | null
  vistoEn: string
}

export interface ColaEquipoDto {
  maxEjecucionesSimultaneas: number | null
  enUso: number
  politica: PoliticaDespacho
  enEjecucion: EnEjecucionDto[]
  pendientes: PendienteDto[]
  robots: RobotDelEquipoDto[]
}

export const getDespachoEquipo = (equipoId: string) => apiFetch<DespachoEquipoDto>(`/equipos/${equipoId}/despacho`)

export const updateDespachoEquipo = (equipoId: string, input: UpdateDespachoInput) =>
  apiFetch<DespachoEquipoDto>(`/equipos/${equipoId}/despacho`, { method: 'PUT', body: JSON.stringify(input) })

export const getColaEquipo = (equipoId: string) => apiFetch<ColaEquipoDto>(`/equipos/${equipoId}/despacho/cola`)

export const listPlantillasDespacho = () => apiFetch<PlantillaDespachoDto[]>('/plantillas-despacho')

export const createPlantillaDespacho = (input: SavePlantillaDespachoInput) =>
  apiFetch<PlantillaDespachoDto>('/plantillas-despacho', { method: 'POST', body: JSON.stringify(input) })

export const updatePlantillaDespacho = (id: string, input: SavePlantillaDespachoInput) =>
  apiFetch<PlantillaDespachoDto>(`/plantillas-despacho/${id}`, { method: 'PUT', body: JSON.stringify(input) })

export const deletePlantillaDespacho = (id: string) => apiFetch<void>(`/plantillas-despacho/${id}`, { method: 'DELETE' })
