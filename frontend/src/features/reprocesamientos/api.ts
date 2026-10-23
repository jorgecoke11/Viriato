import { apiFetch } from '../../lib/apiClient'
import type { PagedResult } from '../../lib/types'

/** How the case's last step ended: the only states a step can be in once the case is over. */
export type EstadoDeEjecucionFinal = 'Fallido' | 'Completado' | 'Cancelado'

export interface ReglaDeReprocesamientoDto {
  id: string
  flujoId: string
  nombre: string
  activa: boolean
  orden: number
  estadoNegocioId: string | null
  estadoNegocioNombre: string | null
  estadoEjecucion: EstadoDeEjecucionFinal | null
  pasoNombre: string | null
  esperaMinutos: number
  limite: number | null
  estadoFinalId: string | null
  estadoFinalNombre: string | null
  createdAt: string
  updatedAt: string
}

export interface GuardarReglaDeReprocesamientoRequest {
  nombre: string
  activa: boolean
  orden: number
  estadoNegocioId: string | null
  estadoEjecucion: EstadoDeEjecucionFinal | null
  pasoNombre: string | null
  esperaMinutos: number
  limite: number | null
  estadoFinalId: string | null
}

export type EstadoDeReprocesamiento = 'Pendiente' | 'Hecho' | 'Fallido' | 'Descartado'

export interface ReprocesamientoProgramadoDto {
  id: string
  reglaId: string | null
  reglaNombre: string
  casoId: string
  casoTitulo: string
  pasoNombre: string | null
  numero: number
  programadoPara: string
  estado: EstadoDeReprocesamiento
  error: string | null
  createdAt: string
  hechoAt: string | null
}

const base = (flujoId: string) => `/flujos/${flujoId}/reprocesamientos`

export const listReglas = (flujoId: string) => apiFetch<ReglaDeReprocesamientoDto[]>(base(flujoId))
export const createRegla = (flujoId: string, input: GuardarReglaDeReprocesamientoRequest) =>
  apiFetch<ReglaDeReprocesamientoDto>(base(flujoId), { method: 'POST', body: JSON.stringify(input) })
export const updateRegla = (flujoId: string, id: string, input: GuardarReglaDeReprocesamientoRequest) =>
  apiFetch<ReglaDeReprocesamientoDto>(`${base(flujoId)}/${id}`, { method: 'PUT', body: JSON.stringify(input) })
export const deleteRegla = (flujoId: string, id: string) => apiFetch<void>(`${base(flujoId)}/${id}`, { method: 'DELETE' })
/** The names of the process's steps (of its active version) a rule can run again. */
export const listPasos = (flujoId: string) => apiFetch<string[]>(`${base(flujoId)}/pasos`)
export const listHistorial = (flujoId: string, filtros: Record<string, string>) =>
  apiFetch<PagedResult<ReprocesamientoProgramadoDto>>(`${base(flujoId)}/historial?${new URLSearchParams(filtros)}`)

/** The rule's form as the kit keeps it: every value a string or a flag. */
export interface ValoresDeRegla {
  [key: string]: string | boolean
  nombre: string
  activa: boolean
  orden: string
  estadoNegocioId: string
  estadoEjecucion: string
  pasoNombre: string
  esperaMinutos: string
  conLimite: boolean
  limite: string
  estadoFinalId: string
}

export const VALORES_VACIOS: ValoresDeRegla = {
  nombre: '',
  activa: true,
  orden: '0',
  estadoNegocioId: '',
  estadoEjecucion: 'Fallido',
  pasoNombre: '',
  esperaMinutos: '0',
  conLimite: false,
  limite: '3',
  estadoFinalId: '',
}

export function aValores(r: ReglaDeReprocesamientoDto): ValoresDeRegla {
  return {
    nombre: r.nombre,
    activa: r.activa,
    orden: String(r.orden),
    estadoNegocioId: r.estadoNegocioId ?? '',
    estadoEjecucion: r.estadoEjecucion ?? '',
    pasoNombre: r.pasoNombre ?? '',
    esperaMinutos: String(r.esperaMinutos),
    conLimite: r.limite !== null,
    limite: r.limite !== null ? String(r.limite) : '3',
    estadoFinalId: r.estadoFinalId ?? '',
  }
}

/** Empty means "any" (states), "the step it ended on" (step) or "no limit" (the box unticked). */
export function aSolicitud(v: ValoresDeRegla): GuardarReglaDeReprocesamientoRequest {
  return {
    nombre: v.nombre.trim(),
    activa: v.activa,
    orden: Number(v.orden) || 0,
    estadoNegocioId: v.estadoNegocioId || null,
    estadoEjecucion: (v.estadoEjecucion || null) as EstadoDeEjecucionFinal | null,
    pasoNombre: v.pasoNombre || null,
    esperaMinutos: Math.max(0, Number(v.esperaMinutos) || 0),
    limite: v.conLimite ? Number(v.limite) || 1 : null,
    estadoFinalId: v.conLimite ? v.estadoFinalId || null : null,
  }
}

export const ESTADOS_DE_EJECUCION: { value: EstadoDeEjecucionFinal; label: string }[] = [
  { value: 'Fallido', label: 'Fallida' },
  { value: 'Completado', label: 'Completada' },
  { value: 'Cancelado', label: 'Cancelada por exceso de tiempo' },
]

/** The rule's condition in words, for the list: «Ejecución fallida · en «Portal caído»». */
export function describirCuando(r: ReglaDeReprocesamientoDto): string {
  const partes = [
    r.estadoEjecucion ? `Ejecución ${ESTADOS_DE_EJECUCION.find((e) => e.value === r.estadoEjecucion)?.label.toLowerCase()}` : null,
    r.estadoNegocioNombre ? `en «${r.estadoNegocioNombre}»` : null,
  ].filter((p): p is string => p !== null)
  const texto = partes.join(' · ')
  return texto.charAt(0).toUpperCase() + texto.slice(1)
}

/** The wait in words: «Al momento», «30 min», «2 h», «1 h 30 min». */
export function describirEspera(minutos: number): string {
  if (minutos <= 0) return 'Al momento'
  const horas = Math.floor(minutos / 60)
  const resto = minutos % 60
  if (horas === 0) return `${resto} min`
  return resto === 0 ? `${horas} h` : `${horas} h ${resto} min`
}
