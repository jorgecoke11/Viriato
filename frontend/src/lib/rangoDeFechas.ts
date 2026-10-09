/**
 * Dates as the user thinks of them: whole days on their own clock, written `yyyy-MM-dd`. Nothing in here knows what the range is
 * for (finished cases, a report, a log): it is the vocabulary the calendar, the picker and whatever filters by date share.
 */

export type Dia = string

/** A way of naming a range that stays true as the days go by ("the last 7 days" is not a pair of dates fixed today). */
export type AtajoDeFecha = 'hoy' | 'ayer' | '7d' | '30d' | 'mes' | 'mesPasado'

export type RangoElegido =
  /** No limit at all. */
  | { tipo: 'todos' }
  | { tipo: 'atajo'; atajo: AtajoDeFecha }
  | { tipo: 'rango'; desde?: Dia; hasta?: Dia }

export const TODOS_LOS_ATAJOS: readonly AtajoDeFecha[] = ['hoy', 'ayer', '7d', '30d', 'mes', 'mesPasado']

const ETIQUETAS: Record<AtajoDeFecha, string> = {
  hoy: 'Hoy',
  ayer: 'Ayer',
  '7d': 'Últimos 7 días',
  '30d': 'Últimos 30 días',
  mes: 'Este mes',
  mesPasado: 'Mes pasado',
}

export const etiquetaDeAtajo = (atajo: AtajoDeFecha): string => ETIQUETAS[atajo]

const dos = (n: number) => String(n).padStart(2, '0')

export function aDia(fecha: Date): Dia {
  return `${fecha.getFullYear()}-${dos(fecha.getMonth() + 1)}-${dos(fecha.getDate())}`
}

/** The day as a Date at local midnight (never through a UTC parse, which would slide it by the time-zone offset). */
export function deDia(dia: Dia): Date {
  const [anio, mes, d] = dia.split('-').map(Number)
  return new Date(anio, mes - 1, d)
}

export function sumarDias(dia: Dia, n: number): Dia {
  const fecha = deDia(dia)
  fecha.setDate(fecha.getDate() + n)
  return aDia(fecha)
}

/** Days compare as text because of how they are written. */
export const compararDias = (a: Dia, b: Dia): number => (a < b ? -1 : a > b ? 1 : 0)

/** The two days in order, whichever way they were given. */
export function ordenarDias(a: Dia, b: Dia): { desde: Dia; hasta: Dia } {
  return compararDias(a, b) <= 0 ? { desde: a, hasta: b } : { desde: b, hasta: a }
}

export function esDia(valor: unknown): valor is Dia {
  if (typeof valor !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(valor)) return false
  return aDia(deDia(valor)) === valor
}

export function resolverAtajo(atajo: AtajoDeFecha, ahora: Date = new Date()): { desde: Dia; hasta: Dia } {
  const hoy = aDia(ahora)
  switch (atajo) {
    case 'hoy':
      return { desde: hoy, hasta: hoy }
    case 'ayer': {
      const ayer = sumarDias(hoy, -1)
      return { desde: ayer, hasta: ayer }
    }
    case '7d':
      return { desde: sumarDias(hoy, -6), hasta: hoy }
    case '30d':
      return { desde: sumarDias(hoy, -29), hasta: hoy }
    case 'mes':
      return { desde: aDia(new Date(ahora.getFullYear(), ahora.getMonth(), 1)), hasta: hoy }
    case 'mesPasado':
      return {
        desde: aDia(new Date(ahora.getFullYear(), ahora.getMonth() - 1, 1)),
        hasta: aDia(new Date(ahora.getFullYear(), ahora.getMonth(), 0)),
      }
  }
}

/** The days a choice stands for today. An open end is simply missing; "todos" is no days at all. */
export function resolverRango(rango: RangoElegido, ahora: Date = new Date()): { desde?: Dia; hasta?: Dia } {
  if (rango.tipo === 'todos') return {}
  if (rango.tipo === 'atajo') return resolverAtajo(rango.atajo, ahora)
  return { desde: rango.desde, hasta: rango.hasta }
}

/** Whether something read back from storage is really one of ours (it may be from an older version of the page). */
export function esRangoElegido(valor: unknown): valor is RangoElegido {
  if (typeof valor !== 'object' || valor === null) return false
  const v = valor as Record<string, unknown>
  if (v.tipo === 'todos') return true
  if (v.tipo === 'atajo') return typeof v.atajo === 'string' && (TODOS_LOS_ATAJOS as readonly string[]).includes(v.atajo)
  if (v.tipo === 'rango') return (v.desde === undefined || esDia(v.desde)) && (v.hasta === undefined || esDia(v.hasta))
  return false
}

/** The start of the given local day, as the instant a server compares against. */
export const inicioDelDia = (dia: Dia): string => new Date(`${dia}T00:00:00`).toISOString()

/** The last instant of the given local day: a range "to the 7th" includes everything that happened on the 7th. */
export const finDelDia = (dia: Dia): string => new Date(`${dia}T23:59:59.999`).toISOString()

// -------------------------------------------------------------------------------------------------- in a URL

/** A choice as the text of a URL parameter: `todas`, a shortcut (`7d`), or `2026-10-01_2026-10-08` ("_" leaves an end open). */
export function rangoAParam(rango: RangoElegido): string {
  if (rango.tipo === 'todos') return 'todas'
  if (rango.tipo === 'atajo') return rango.atajo
  return `${rango.desde ?? ''}_${rango.hasta ?? ''}`
}

/** What `rangoAParam` wrote, or null if the text is not one of ours (a link typed by hand, or from an older version). */
export function rangoDeParam(texto: string | null | undefined): RangoElegido | null {
  if (!texto) return null
  if (texto === 'todas') return { tipo: 'todos' }
  if ((TODOS_LOS_ATAJOS as readonly string[]).includes(texto)) return { tipo: 'atajo', atajo: texto as AtajoDeFecha }
  const partes = texto.split('_')
  if (partes.length !== 2) return null
  const [desde, hasta] = partes
  if ((desde !== '' && !esDia(desde)) || (hasta !== '' && !esDia(hasta)) || (desde === '' && hasta === '')) return null
  return { tipo: 'rango', desde: desde || undefined, hasta: hasta || undefined }
}

// -------------------------------------------------------------------------------------------------- reading a range

function corto(dia: Dia, conAnio: boolean): string {
  const fecha = deDia(dia)
  return new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'short', ...(conAnio ? { year: 'numeric' } : {}) })
    .format(fecha)
    .replace(/\./g, '')
}

/** "8 oct", "1 – 8 oct", "28 sep – 3 oct": the year only when it is not the current one, or when the two ends differ in it. */
export function describirDias(desde: Dia | undefined, hasta: Dia | undefined, ahora: Date = new Date()): string {
  if (desde && hasta) {
    if (desde === hasta) return corto(desde, deDia(desde).getFullYear() !== ahora.getFullYear())
    const anioA = deDia(desde).getFullYear()
    const anioB = deDia(hasta).getFullYear()
    const conAnio = anioA !== anioB || anioB !== ahora.getFullYear()
    return `${corto(desde, anioA !== anioB)} – ${corto(hasta, conAnio)}`
  }
  if (desde) return `Desde el ${corto(desde, deDia(desde).getFullYear() !== ahora.getFullYear())}`
  if (hasta) return `Hasta el ${corto(hasta, deDia(hasta).getFullYear() !== ahora.getFullYear())}`
  return 'Sin fechas'
}

/** What a choice reads as on a button: the name of a shortcut, the dates of a range, or the word for "no limit". */
export function describirRango(rango: RangoElegido, ahora: Date = new Date(), textoDeTodos = 'Todo'): string {
  if (rango.tipo === 'todos') return textoDeTodos
  if (rango.tipo === 'atajo') return etiquetaDeAtajo(rango.atajo)
  return describirDias(rango.desde, rango.hasta, ahora)
}

// -------------------------------------------------------------------------------------------------- the calendar

export interface DiaDeCalendario {
  dia: Dia
  /** Belongs to the month being shown (the rest are the neighbours that fill the first and last weeks). */
  delMes: boolean
}

/** The weeks of a month, Monday first, always six rows so the calendar does not change height from month to month. */
export function semanasDelMes(anio: number, mes: number): DiaDeCalendario[][] {
  const primero = new Date(anio, mes, 1)
  const desfase = (primero.getDay() + 6) % 7 // Monday = 0
  const inicio = new Date(anio, mes, 1 - desfase)
  const semanas: DiaDeCalendario[][] = []
  for (let s = 0; s < 6; s++) {
    const semana: DiaDeCalendario[] = []
    for (let d = 0; d < 7; d++) {
      const fecha = new Date(inicio.getFullYear(), inicio.getMonth(), inicio.getDate() + s * 7 + d)
      semana.push({ dia: aDia(fecha), delMes: fecha.getMonth() === mes })
    }
    semanas.push(semana)
  }
  return semanas
}
