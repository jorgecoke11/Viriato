import type { CasoTimelineItemDto } from './api'

/**
 * What the history of a Caso is made of, as far as how it is drawn: a status change, a screenshot, a video, a file, extracted
 * data or a note — A file is a file
 * whether a person uploaded it or a step produced it.
 */
export type TipoDeEntrada = 'estado' | 'captura' | 'video' | 'archivo' | 'datos' | 'nota'

export type FiltroDeLinea = 'todo' | TipoDeEntrada

export function tipoDeEntrada(item: CasoTimelineItemDto): TipoDeEntrada {
  if (item.tipo === 'EstadoCambiado') return 'estado'
  // A document someone uploaded is a file to download, however it looks; only a step's evidence is looked at.
  if (item.tipo === 'Documento') return 'archivo'
  if (item.documentoId !== null) {
    if (item.evidenciaTipo === 'Screenshot' || item.contentType?.startsWith('image/')) return 'captura'
    if (item.evidenciaTipo === 'Video' || item.contentType?.startsWith('video/')) return 'video'
    return 'archivo'
  }
  return item.evidenciaTipo === 'DatosExtraidos' ? 'datos' : 'nota'
}

export type EntradaDeLinea =
  | { clase: 'item'; id: string; tipo: TipoDeEntrada; item: CasoTimelineItemDto }
  /** Screenshots one after another from the same step, shown as one contact sheet instead of a card each. */
  | { clase: 'galeria'; id: string; items: CasoTimelineItemDto[]; pasoNombre: string | null }

export interface DiaDeLinea {
  clave: string
  /** "Hoy", "Ayer" or the weekday. */
  etiqueta: string
  /** The date, for the line beside the label. */
  detalle: string
  entradas: EntradaDeLinea[]
}

export function contarPorTipo(items: readonly CasoTimelineItemDto[]): Record<FiltroDeLinea, number> {
  const cuenta: Record<FiltroDeLinea, number> = { todo: items.length, estado: 0, captura: 0, video: 0, archivo: 0, datos: 0, nota: 0 }
  for (const item of items) cuenta[tipoDeEntrada(item)] += 1
  return cuenta
}

const claveDeDia = (fecha: Date) => `${fecha.getFullYear()}-${fecha.getMonth() + 1}-${fecha.getDate()}`

function etiquetaDelDia(fecha: Date, ahora: Date): { etiqueta: string; detalle: string } {
  const detalle = new Intl.DateTimeFormat('es-ES', { day: 'numeric', month: 'short', ...(fecha.getFullYear() !== ahora.getFullYear() ? { year: 'numeric' } : {}) })
    .format(fecha)
    .replace('.', '')
  const ayer = new Date(ahora)
  ayer.setDate(ayer.getDate() - 1)
  if (claveDeDia(fecha) === claveDeDia(ahora)) return { etiqueta: 'Hoy', detalle }
  if (claveDeDia(fecha) === claveDeDia(ayer)) return { etiqueta: 'Ayer', detalle }
  const diaDeLaSemana = new Intl.DateTimeFormat('es-ES', { weekday: 'long' }).format(fecha)
  return { etiqueta: diaDeLaSemana.charAt(0).toUpperCase() + diaDeLaSemana.slice(1), detalle }
}

/**
 * The history as the screen draws it: narrowed to one kind of entry, newest first, split by day, and with the screenshots that
 * a step took one after another folded into a single gallery (two or more, same step, no other entry between them).
 */
export function construirLinea(items: readonly CasoTimelineItemDto[], filtro: FiltroDeLinea, ahora: Date = new Date()): DiaDeLinea[] {
  const visibles = items
    .filter((i) => filtro === 'todo' || tipoDeEntrada(i) === filtro)
    .sort((a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime())

  const dias: DiaDeLinea[] = []
  for (const item of visibles) {
    const fecha = new Date(item.occurredAt)
    const clave = claveDeDia(fecha)
    let dia = dias[dias.length - 1]
    if (dia === undefined || dia.clave !== clave) {
      dia = { clave, ...etiquetaDelDia(fecha, ahora), entradas: [] }
      dias.push(dia)
    }

    const tipo = tipoDeEntrada(item)
    const anterior = dia.entradas[dia.entradas.length - 1]
    const sigueLaGaleria =
      tipo === 'captura' &&
      anterior !== undefined &&
      ((anterior.clase === 'galeria' && anterior.pasoNombre === item.pasoNombre) ||
        (anterior.clase === 'item' && anterior.tipo === 'captura' && anterior.item.pasoNombre === item.pasoNombre))

    if (!sigueLaGaleria) {
      dia.entradas.push({ clase: 'item', id: item.id, tipo, item })
    } else if (anterior.clase === 'galeria') {
      anterior.items.push(item)
    } else {
      dia.entradas[dia.entradas.length - 1] = { clase: 'galeria', id: `galeria-${anterior.id}`, items: [anterior.item, item], pasoNombre: item.pasoNombre }
    }
  }

  // Inside a gallery the shots go in the order they were taken, which is also the order the viewer goes through them.
  for (const dia of dias) {
    for (const entrada of dia.entradas) {
      if (entrada.clase === 'galeria') entrada.items.reverse()
    }
  }
  return dias
}
