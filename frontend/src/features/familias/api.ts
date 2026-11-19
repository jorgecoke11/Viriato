import { apiFetch } from '../../lib/apiClient'
import type { PagedResult } from '../../lib/types'

export type CondicionDeCierre = 'Siempre' | 'SoloSiTodosBien'
export type EstadoDeFamilia = 'Abierta' | 'Cerrada' | 'Terminada'

export interface TipoDeFamiliaDto {
  id: string
  flujoId: string
  nombre: string
  descripcion: string | null
  activo: boolean
  creadorAlTerminarId: string | null
  creadorAlTerminarNombre: string | null
  creadorAlTerminarProceso: string | null
  condicion: CondicionDeCierre
  /** Its families form by themselves with the cases a case creates from this step; null: robots or people create them. */
  pasoQueLaForma: string | null
  unaPorTipoDeCaso: boolean
  createdAt: string
  updatedAt: string
}

export interface GuardarTipoDeFamiliaRequest {
  nombre: string
  descripcion: string | null
  activo: boolean
  creadorAlTerminarId: string | null
  condicion: CondicionDeCierre
  pasoQueLaForma: string | null
  unaPorTipoDeCaso: boolean
}

export interface FamiliaDto {
  id: string
  flujoId: string
  flujoNombre: string
  tipoDeFamiliaId: string
  tipo: string
  nombre: string
  estado: EstadoDeFamilia
  participantesEsperados: number | null
  miembros: number
  terminados: number
  completados: number | null
  fallidos: number | null
  cancelados: number | null
  casoDeCierreId: string | null
  casoDeCierreTitulo: string | null
  notaDeCierre: string | null
  createdAt: string
  cerradaAt: string | null
  terminadaAt: string | null
}

export type AccionDeFamilia = 'Creada' | 'Cerrada' | 'Terminada' | 'CierreAbierto' | 'CierreNoAbierto' | 'NotificacionEnviada' | 'NotificacionFallida'

export interface FamiliaEventoDto {
  id: string
  accion: AccionDeFamilia
  detalleJson: string | null
  occurredAt: string
}

const tipos = (flujoId: string) => `/flujos/${flujoId}/tipos-de-familia`

export const listTipos = (flujoId: string) => apiFetch<TipoDeFamiliaDto[]>(tipos(flujoId))
export const createTipo = (flujoId: string, input: GuardarTipoDeFamiliaRequest) =>
  apiFetch<TipoDeFamiliaDto>(tipos(flujoId), { method: 'POST', body: JSON.stringify(input) })
export const updateTipo = (flujoId: string, id: string, input: GuardarTipoDeFamiliaRequest) =>
  apiFetch<TipoDeFamiliaDto>(`${tipos(flujoId)}/${id}`, { method: 'PUT', body: JSON.stringify(input) })
export const deleteTipo = (flujoId: string, id: string) => apiFetch<void>(`${tipos(flujoId)}/${id}`, { method: 'DELETE' })

export const listFamilias = (filtros: Record<string, string | undefined>) => {
  const parametros = new URLSearchParams()
  for (const [clave, valor] of Object.entries(filtros)) if (valor) parametros.set(clave, valor)
  return apiFetch<PagedResult<FamiliaDto>>(`/familias?${parametros}`)
}
export const getFamilia = (id: string) => apiFetch<FamiliaDto>(`/familias/${id}`)
export const listEventos = (id: string) => apiFetch<FamiliaEventoDto[]>(`/familias/${id}/eventos`)
export const cerrarFamilia = (id: string) => apiFetch<FamiliaDto>(`/familias/${id}/cerrar`, { method: 'POST' })
export const familiasDelCaso = (casoId: string) => apiFetch<FamiliaDto[]>(`/casos/${casoId}/familias`)

/** How far along it is, 0–100: of the cases it expects if it says how many, otherwise of the ones it has. */
export function progreso(f: Pick<FamiliaDto, 'miembros' | 'terminados' | 'participantesEsperados'>): number {
  const total = Math.max(f.participantesEsperados ?? 0, f.miembros)
  return total === 0 ? 0 : (f.terminados / total) * 100
}

/** «3 de 5 terminados», «3 de 5 terminados · esperando 2 más». */
export function describirProgreso(f: Pick<FamiliaDto, 'miembros' | 'terminados' | 'participantesEsperados' | 'estado'>): string {
  const base = `${f.terminados} de ${f.miembros} terminado${f.miembros === 1 ? '' : 's'}`
  const faltan = f.estado === 'Abierta' && f.participantesEsperados !== null ? f.participantesEsperados - f.miembros : 0
  return faltan > 0 ? `${base} · esperando ${faltan} más` : base
}

export const ETIQUETAS_DE_ESTADO: Record<EstadoDeFamilia, string> = {
  Abierta: 'Abierta',
  Cerrada: 'Cerrada, esperando a sus casos',
  Terminada: 'Terminada',
}

export const CONDICIONES: { value: CondicionDeCierre; label: string }[] = [
  { value: 'Siempre', label: 'Siempre, acaben como acaben sus casos' },
  { value: 'SoloSiTodosBien', label: 'Solo si todos sus casos acabaron completados' },
]

function leer(json: string | null): Record<string, unknown> {
  if (!json) return {}
  try {
    const valor = JSON.parse(json) as unknown
    return valor && typeof valor === 'object' ? (valor as Record<string, unknown>) : {}
  } catch {
    return {}
  }
}

const plural = (n: number, uno: string, varios: string) => `${n} ${n === 1 ? uno : varios}`

/** How its cases ended: «2 completados · 1 fallido · 0 cancelados». */
export function describirFinal(f: Pick<FamiliaDto, 'completados' | 'fallidos' | 'cancelados'>): string {
  return [
    plural(f.completados ?? 0, 'completado', 'completados'),
    plural(f.fallidos ?? 0, 'fallido', 'fallidos'),
    plural(f.cancelados ?? 0, 'cancelado', 'cancelados'),
  ].join(' · ')
}

/** A line of the family's history in words. */
export function describirEvento(e: Pick<FamiliaEventoDto, 'accion' | 'detalleJson'>): string {
  const d = leer(e.detalleJson)
  switch (e.accion) {
    case 'Creada':
      if (d.paso) return `Se formó sola con los casos que crea el paso «${String(d.paso)}»`
      return d.participantesEsperados ? `Creada, esperando ${String(d.participantesEsperados)} casos` : 'Creada'
    case 'Cerrada':
      if (d.porPaso) return `Se cerró al terminar el paso que creaba sus casos (${String(d.miembros ?? 0)} casos)`
      return d.automatica ? `Se cerró sola al llegar a ${String(d.miembros)} casos` : `Cerrada con ${String(d.miembros ?? 0)} casos`
    case 'Terminada':
      return `Terminada: ${describirFinal({ completados: Number(d.completados ?? 0), fallidos: Number(d.fallidos ?? 0), cancelados: Number(d.cancelados ?? 0) })}`
    case 'CierreAbierto':
      return 'Abrió su caso de cierre'
    case 'CierreNoAbierto':
      return `Sin caso de cierre: ${String(d.motivo ?? '')}`
    case 'NotificacionEnviada':
      return `Aviso «${String(d.regla ?? '')}» enviado`
    case 'NotificacionFallida':
      return `No se pudo enviar el aviso «${String(d.regla ?? '')}»`
  }
}
