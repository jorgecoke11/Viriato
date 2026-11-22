import type { FlujoVersionDetailDto, ReplacePasosRequest, ResultadoEjecucion, TipoPaso } from './api'

/** The flow editor's model, independent of the canvas library: steps are boxes, arrows join them. */

export const INICIO = '__inicio__'

export interface PasoDelGrafo {
  /** Stable within the editor; becomes the step's `clave` when saving. */
  clave: string
  nombre: string
  tipoPaso: TipoPaso
  servicioId: string | null
  agenteDefinicionId: string | null
  configuracionJson: string | null
  maximoDeVisitas: number | null
  x: number
  y: number
}

export interface FlechaDelGrafo {
  id: string
  /** `INICIO` for the start arrow. */
  origen: string
  destino: string
  /** Null only on the start arrow. */
  resultado: ResultadoEjecucion | null
  /** Null: any business state. */
  estado: string | null
}

export interface Grafo {
  pasos: PasoDelGrafo[]
  flechas: FlechaDelGrafo[]
}

export const ANCHO_COLUMNA = 300
export const ALTO_FILA = 140
export const POSICION_INICIO = { x: 0, y: 0 }

/** Same rule as the server (and the admin screen for states). */
export const CODIGO_DE_ESTADO = /^[A-Za-z0-9_-]{1,50}$/

let siguiente = 0
export const nuevaClave = (prefijo = 'paso') => `${prefijo}-${Date.now().toString(36)}-${++siguiente}`

/**
 * The editor's graph for a version. A version with arrows is read as it is. One without them (from before arrows existed) is
 * turned into the arrows it already behaves like — start → first step, each step → the next one when it ends Completada — so
 * a draft can be carried over to the new editor. Returns null when that is not possible (it has Decision steps, whose branching
 * the arrows express differently), and the caller keeps showing it as a list.
 */
export function grafoDesdeVersion(version: FlujoVersionDetailDto): Grafo | null {
  const pasos = [...version.pasos].sort((a, b) => a.orden - b.orden)

  if (version.transiciones.length === 0 && pasos.some((p) => p.tipoPaso === 'Decision')) return null

  const delGrafo: PasoDelGrafo[] = pasos.map((p, i) => ({
    clave: p.id,
    nombre: p.nombre,
    tipoPaso: p.tipoPaso,
    servicioId: p.servicioId,
    agenteDefinicionId: p.agenteDefinicionId,
    configuracionJson: p.tipoPaso === 'Rpa' ? null : p.configuracionJson,
    maximoDeVisitas: p.maximoDeVisitas,
    x: p.posicionX ?? (i + 1) * ANCHO_COLUMNA,
    y: p.posicionY ?? 0,
  }))

  if (version.transiciones.length > 0) {
    return {
      pasos: delGrafo,
      flechas: version.transiciones.map((t) => ({
        id: t.id,
        origen: t.pasoOrigenId ?? INICIO,
        destino: t.pasoDestinoId,
        resultado: t.pasoOrigenId === null ? null : t.resultado,
        estado: t.estadoNegocioCodigo,
      })),
    }
  }

  const flechas: FlechaDelGrafo[] = []
  if (delGrafo.length > 0) flechas.push({ id: nuevaClave('flecha'), origen: INICIO, destino: delGrafo[0].clave, resultado: null, estado: null })
  for (let i = 0; i + 1 < delGrafo.length; i++) {
    flechas.push({ id: nuevaClave('flecha'), origen: delGrafo[i].clave, destino: delGrafo[i + 1].clave, resultado: 'Completada', estado: null })
  }
  return { pasos: delGrafo, flechas }
}

/** The save request. Steps are numbered left to right, top to bottom, which is how lists of steps elsewhere show them. */
export function peticionDesdeGrafo(grafo: Grafo): ReplacePasosRequest {
  const ordenados = [...grafo.pasos].sort((a, b) => a.x - b.x || a.y - b.y)
  return {
    pasos: ordenados.map((p, i) => ({
      clave: p.clave,
      orden: i + 1,
      nombre: p.nombre.trim(),
      tipoPaso: p.tipoPaso,
      servicioId: p.tipoPaso === 'Rpa' ? p.servicioId : null,
      agenteDefinicionId: p.tipoPaso === 'Agente' ? p.agenteDefinicionId : null,
      configuracionJson: p.tipoPaso === 'Rpa' ? null : p.configuracionJson?.trim() || null,
      posicionX: Math.round(p.x),
      posicionY: Math.round(p.y),
      maximoDeVisitas: p.maximoDeVisitas,
    })),
    transiciones: grafo.flechas.map((f) => ({
      origen: f.origen === INICIO ? null : f.origen,
      destino: f.destino,
      resultado: f.origen === INICIO ? null : f.resultado,
      estadoNegocioCodigo: f.origen === INICIO ? null : f.estado?.trim() || null,
    })),
  }
}

/** A creator of the process that opens its Casos at a step, by the step's name. */
export interface EntradaDeCreador {
  creador: string
  paso: string
}

const mismoNombre = (a: string, b: string) => a.trim().localeCompare(b.trim(), undefined, { sensitivity: 'accent' }) === 0

/** The creators that open their Casos at this step. */
export function creadoresQueEmpiezanEn(paso: PasoDelGrafo, entradas: EntradaDeCreador[]): string[] {
  return entradas.filter((e) => mismoNombre(e.paso, paso.nombre)).map((e) => e.creador)
}

/** The steps some arrow path reaches from the start or from a step a creator starts at (a process may hold several flows). */
export function alcanzables(grafo: Grafo, entradas: EntradaDeCreador[] = []): Set<string> {
  const vistos = new Set<string>()
  const pendientes = [
    ...grafo.flechas.filter((f) => f.origen === INICIO).map((f) => f.destino),
    ...grafo.pasos.filter((p) => creadoresQueEmpiezanEn(p, entradas).length > 0).map((p) => p.clave),
  ]
  while (pendientes.length > 0) {
    const paso = pendientes.pop()!
    if (vistos.has(paso)) continue
    vistos.add(paso)
    for (const f of grafo.flechas) if (f.origen === paso) pendientes.push(f.destino)
  }
  return vistos
}

/** Arrows that clash: two leaving the same step for the same result and state. The server refuses them. */
export function flechasRepetidas(grafo: Grafo): Set<string> {
  const porCombinacion = new Map<string, string[]>()
  for (const f of grafo.flechas) {
    if (f.origen === INICIO) continue
    const clave = `${f.origen}|${f.resultado}|${f.estado?.trim() ?? ''}`
    porCombinacion.set(clave, [...(porCombinacion.get(clave) ?? []), f.id])
  }
  return new Set([...porCombinacion.values()].filter((ids) => ids.length > 1).flat())
}

export interface Aviso {
  /** The step or arrow it is about, to highlight it; null for the whole flow. */
  objetivo: string | null
  texto: string
  /** Saving is refused while it stands (publishing is refused for the rest). */
  bloquea: boolean
}

export function avisos(grafo: Grafo, entradas: EntradaDeCreador[] = []): Aviso[] {
  const lista: Aviso[] = []
  if (grafo.pasos.length === 0) lista.push({ objetivo: null, texto: 'Añade al menos un paso.', bloquea: true })
  if (!grafo.flechas.some((f) => f.origen === INICIO) && grafo.pasos.length > 0) {
    lista.push({ objetivo: INICIO, texto: 'Une Inicio con el paso por el que empiezan los casos.', bloquea: true })
  }

  const servicios = new Map<string, number>()
  for (const p of grafo.pasos) {
    if (!p.nombre.trim()) lista.push({ objetivo: p.clave, texto: 'Un paso no tiene nombre.', bloquea: true })
    if (p.tipoPaso === 'Rpa' && !p.servicioId) lista.push({ objetivo: p.clave, texto: `«${p.nombre}» no tiene servicio.`, bloquea: true })
    if (p.tipoPaso === 'Agente' && !p.agenteDefinicionId) lista.push({ objetivo: p.clave, texto: `«${p.nombre}» no tiene agente.`, bloquea: true })
    if (p.servicioId) servicios.set(p.servicioId, (servicios.get(p.servicioId) ?? 0) + 1)
  }
  for (const p of grafo.pasos) {
    if (p.servicioId && (servicios.get(p.servicioId) ?? 0) > 1) {
      lista.push({ objetivo: p.clave, texto: `El servicio de «${p.nombre}» está dos veces en el flujo.`, bloquea: true })
    }
  }

  for (const f of grafo.flechas) {
    if (f.estado && !CODIGO_DE_ESTADO.test(f.estado.trim())) {
      lista.push({ objetivo: f.id, texto: `«${f.estado}» no es un código de estado válido (letras, números, _ y -).`, bloquea: true })
    }
  }
  for (const id of flechasRepetidas(grafo)) {
    lista.push({ objetivo: id, texto: 'Dos flechas salen del mismo paso con el mismo resultado y estado.', bloquea: true })
  }

  const llegan = alcanzables(grafo, entradas)
  for (const p of grafo.pasos) {
    if (!llegan.has(p.clave)) {
      lista.push({
        objetivo: p.clave,
        texto: `Ni una flecha ni un creador llevan a «${p.nombre || 'sin nombre'}». No se podrá publicar.`,
        bloquea: false,
      })
    }
  }

  // Creators look their step up by name: one whose step is not in this flow would refuse every Caso once it is published.
  for (const e of entradas) {
    if (!grafo.pasos.some((p) => mismoNombre(p.nombre, e.paso))) {
      lista.push({
        objetivo: null,
        texto: `El creador «${e.creador}» empieza en «${e.paso}», que no está en este flujo. Llama así a un paso o cambia el creador; si no, no se podrá publicar.`,
        bloquea: false,
      })
    }
  }
  return lista
}

/** Lays the steps out in columns by distance from the start (left to right), the ones no arrow reaches in a last column. */
export function ordenar(grafo: Grafo): Grafo {
  const nivel = new Map<string, number>()
  const cola: [string, number][] = grafo.flechas.filter((f) => f.origen === INICIO).map((f) => [f.destino, 1])
  while (cola.length > 0) {
    const [paso, n] = cola.shift()!
    if (nivel.has(paso)) continue
    nivel.set(paso, n)
    for (const f of grafo.flechas) if (f.origen === paso && !nivel.has(f.destino)) cola.push([f.destino, n + 1])
  }
  const ultimo = Math.max(0, ...nivel.values()) + 1
  const filas = new Map<number, number>()
  return {
    ...grafo,
    pasos: grafo.pasos.map((p) => {
      const columna = nivel.get(p.clave) ?? ultimo
      const fila = filas.get(columna) ?? 0
      filas.set(columna, fila + 1)
      return { ...p, x: columna * ANCHO_COLUMNA, y: fila * ALTO_FILA }
    }),
  }
}

/** What an arrow says, short enough for its label. */
export function etiquetaDeFlecha(f: FlechaDelGrafo): string {
  if (f.origen === INICIO) return 'Inicio'
  return f.estado?.trim() || 'cualquier estado'
}
