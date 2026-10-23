import { apiFetch } from '../../lib/apiClient'
import type { PagedResult } from '../../lib/types'

export type Disparador = 'EventoDelRobot' | 'EstadoNegocio' | 'CasoFallido' | 'CasoCompletado'

export interface ReglaDeNotificacionDto {
  id: string
  flujoId: string
  tipoCasoId: string | null
  tipoCasoNombre: string | null
  nombre: string
  activa: boolean
  disparador: Disparador
  codigo: string | null
  conexionId: string
  conexionNombre: string
  destinatarios: string[]
  asunto: string
  cuerpo: string
  createdAt: string
  updatedAt: string
}

export interface GuardarReglaRequest {
  nombre: string
  activa: boolean
  disparador: Disparador
  codigo: string | null
  tipoCasoId: string | null
  conexionId: string
  destinatarios: string[]
  asunto: string
  cuerpo: string
}

export type EstadoDeEnvio = 'Pendiente' | 'Enviado' | 'Fallido'

export interface EnvioDeNotificacionDto {
  id: string
  reglaId: string | null
  reglaNombre: string
  casoId: string
  casoTitulo: string
  destinatarios: string[]
  asunto: string
  estado: EstadoDeEnvio
  intentos: number
  ultimoError: string | null
  createdAt: string
  enviadoAt: string | null
}

export interface ResultadoDePruebaDeRegla {
  enviado: boolean
  destinatarios: string[]
  asunto: string
  cuerpo: string
  error?: string
}

const base = (flujoId: string) => `/flujos/${flujoId}/notificaciones`

export const listReglas = (flujoId: string) => apiFetch<ReglaDeNotificacionDto[]>(base(flujoId))
export const createRegla = (flujoId: string, input: GuardarReglaRequest) =>
  apiFetch<ReglaDeNotificacionDto>(base(flujoId), { method: 'POST', body: JSON.stringify(input) })
export const updateRegla = (flujoId: string, id: string, input: GuardarReglaRequest) =>
  apiFetch<ReglaDeNotificacionDto>(`${base(flujoId)}/${id}`, { method: 'PUT', body: JSON.stringify(input) })
export const deleteRegla = (flujoId: string, id: string) => apiFetch<void>(`${base(flujoId)}/${id}`, { method: 'DELETE' })
export const probarRegla = (flujoId: string, id: string, destinatario: string | null) =>
  apiFetch<ResultadoDePruebaDeRegla>(`${base(flujoId)}/${id}/probar`, { method: 'POST', body: JSON.stringify({ destinatario }) })

export const listEnvios = (flujoId: string, filtros: Record<string, string>) =>
  apiFetch<PagedResult<EnvioDeNotificacionDto>>(`${base(flujoId)}/envios?${new URLSearchParams(filtros)}`)
export const reintentarEnvio = (flujoId: string, id: string) => apiFetch<void>(`${base(flujoId)}/envios/${id}/reintentar`, { method: 'POST' })

/** The addresses written one per line or separated by commas or semicolons. */
export function leerDestinatarios(texto: string): string[] {
  return [...new Set(texto.split(/[\n,;]+/).map((d) => d.trim()).filter((d) => d.length > 0))]
}
