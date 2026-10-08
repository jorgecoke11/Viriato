import type { CasoListItemDto } from './api'

const MS_MINUTO = 60_000

/** "8 oct, 11:52" — the year only when it is not the current one. Short enough for a table cell, and the same
 *  everywhere a Caso date is shown, so two dates can be compared at a glance. */
export function formatFecha(iso: string, ahora: Date = new Date()): string {
  const fecha = new Date(iso)
  const otroAnio = fecha.getFullYear() !== ahora.getFullYear()
  return new Intl.DateTimeFormat('es-ES', {
    day: 'numeric',
    month: 'short',
    ...(otroAnio ? { year: 'numeric' } : {}),
    hour: '2-digit',
    minute: '2-digit',
  })
    .format(fecha)
    .replace('.', '')
}

/** The complete date for a tooltip. */
export function formatFechaCompleta(iso: string): string {
  return new Date(iso).toLocaleString('es-ES', { dateStyle: 'full', timeStyle: 'medium' })
}

/** "hace 5 min", "hace 3 h", "hace 2 d" — how long ago, for the line under a date. */
export function haceCuanto(iso: string, ahora: Date = new Date()): string {
  const minutos = Math.max(0, Math.round((ahora.getTime() - new Date(iso).getTime()) / MS_MINUTO))
  if (minutos < 1) return 'ahora mismo'
  if (minutos < 60) return `hace ${minutos} min`
  const horas = Math.round(minutos / 60)
  if (horas < 48) return `hace ${horas} h`
  return `hace ${Math.round(horas / 24)} d`
}

export interface UltimoResultado {
  /** When the Caso last finished — completed, failed or cancelled. Null while it is moving. */
  finalizadoAt: string | null
  /** The last time anything happened to it (a reprocess counts), always set. */
  actividadAt: string
}

/**
 * What the table shows for "when did it end". A Caso that finished has one date, and it is always the latest finish:
 * reprocessing it clears the date, and finishing again sets a new one. While it is being reprocessed there is no
 * finish date, so the table shows that it is moving and when it last did anything instead of a blank.
 */
export function ultimoResultado(caso: Pick<CasoListItemDto, 'completedAt' | 'updatedAt'>): UltimoResultado {
  return { finalizadoAt: caso.completedAt, actividadAt: caso.updatedAt }
}
