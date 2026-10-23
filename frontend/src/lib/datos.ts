export type ValorDeDato =
  | { tipo: 'texto'; texto: string }
  | { tipo: 'numero'; texto: string }
  | { tipo: 'booleano'; texto: string }
  | { tipo: 'vacio' }
  /** An object or a list: shown folded, with what it holds said in a few words and the JSON behind it. A list of records (objects
   *  of plain values, like a history of readings) also comes as a table. */
  | { tipo: 'compuesto'; resumen: string; json: string; tabla?: TablaDeDatos }

export interface TablaDeDatos {
  columnas: { clave: string; etiqueta: string }[]
  /** One row per record, a text per column ('' where the record has nothing). */
  filas: string[][]
}

export interface EntradaDeDato {
  clave: string
  etiqueta: string
  valor: ValorDeDato
}

export type Datos =
  | { tipo: 'vacio' }
  /** What the business data says, one entry per field. */
  | { tipo: 'entradas'; entradas: EntradaDeDato[] }
  /** Not a JSON object (or not JSON at all): shown as it is. */
  | { tipo: 'crudo'; texto: string }

/** "precioUnitario" and "precio_unitario" both read "Precio unitario". */
export function etiquetaDeClave(clave: string): string {
  const separado = clave
    .replace(/[_-]+/g, ' ')
    .replace(/([a-záéíóúñ0-9])([A-ZÁÉÍÓÚÑ])/g, '$1 $2')
    .trim()
    .toLowerCase()
  return separado.charAt(0).toUpperCase() + separado.slice(1)
}

const MAXIMO_DE_COLUMNAS = 8
const FECHA_Y_HORA = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/

const esRegistro = (v: unknown): v is Record<string, unknown> =>
  typeof v === 'object' && v !== null && !Array.isArray(v) && Object.values(v).every((x) => x === null || typeof x !== 'object')

/** A plain value as one short text: a moment in time in local terms, everything else as it is. */
function textoDe(valor: unknown): string {
  if (valor === null || valor === undefined) return ''
  if (typeof valor === 'boolean') return valor ? 'Sí' : 'No'
  if (typeof valor === 'string' && FECHA_Y_HORA.test(valor)) {
    const fecha = new Date(valor)
    if (!Number.isNaN(fecha.getTime())) return fecha.toLocaleString('es-ES', { dateStyle: 'short', timeStyle: 'short' })
  }
  return String(valor)
}

/** A list whose elements are all records with few fields reads better as a table; anything else does not get one. */
function tablaDe(lista: unknown[]): TablaDeDatos | undefined {
  if (lista.length === 0 || !lista.every(esRegistro)) return undefined
  const claves = [...new Set(lista.flatMap((r) => Object.keys(r as object)))]
  if (claves.length === 0 || claves.length > MAXIMO_DE_COLUMNAS) return undefined
  return {
    columnas: claves.map((clave) => ({ clave, etiqueta: etiquetaDeClave(clave) })),
    filas: lista.map((r) => claves.map((c) => textoDe((r as Record<string, unknown>)[c]))),
  }
}

function valorDe(valor: unknown): ValorDeDato {
  if (valor === null || valor === undefined || valor === '') return { tipo: 'vacio' }
  if (typeof valor === 'string') return { tipo: 'texto', texto: valor }
  if (typeof valor === 'number') return { tipo: 'numero', texto: String(valor) }
  if (typeof valor === 'boolean') return { tipo: 'booleano', texto: valor ? 'Sí' : 'No' }
  const json = JSON.stringify(valor, null, 2)
  if (Array.isArray(valor)) {
    return { tipo: 'compuesto', resumen: valor.length === 1 ? '1 elemento' : `${valor.length} elementos`, json, tabla: tablaDe(valor) }
  }
  const campos = Object.keys(valor as object).length
  return { tipo: 'compuesto', resumen: campos === 1 ? '1 campo' : `${campos} campos`, json }
}

/** Reads a JSON document (a case's business data, what a robot keeps…) into what the screen shows: one entry per field. */
export function leerDatos(datosJson: string | null): Datos {
  if (datosJson === null || datosJson.trim() === '') return { tipo: 'vacio' }

  let analizado: unknown
  try {
    analizado = JSON.parse(datosJson)
  } catch {
    return { tipo: 'crudo', texto: datosJson }
  }

  if (analizado === null || typeof analizado !== 'object' || Array.isArray(analizado)) {
    return { tipo: 'crudo', texto: JSON.stringify(analizado, null, 2) }
  }

  const entradas = Object.entries(analizado as Record<string, unknown>).map(([clave, valor]) => ({
    clave,
    etiqueta: etiquetaDeClave(clave),
    valor: valorDe(valor),
  }))
  return entradas.length === 0 ? { tipo: 'vacio' } : { tipo: 'entradas', entradas }
}
