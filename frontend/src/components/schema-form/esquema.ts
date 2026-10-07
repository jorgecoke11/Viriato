// A JSON document described as a form. This is a small subset of JSON Schema — enough to draw a form and to check
// what the user typed — and it mirrors, rule for rule, the server's EsquemaDatos (backend, Viariato.Modules.Flujos):
// what this accepts, the server accepts, so a form that submits cleanly is never turned down for its data.
// Keep both in step. The order of `properties` is the order of the fields.

export type TipoCampo = 'string' | 'number' | 'integer' | 'boolean' | 'array' | 'object'

export interface CampoEsquema {
  type: TipoCampo
  title?: string
  description?: string
  default?: unknown
  enum?: (string | number)[]
  enumNames?: string[]
  minLength?: number
  maxLength?: number
  pattern?: string
  format?: 'date' | 'date-time' | 'email'
  'x-widget'?: 'textarea'
  'x-suffix'?: string
  minimum?: number
  maximum?: number
  items?: CampoEsquema
  minItems?: number
  maxItems?: number
  properties?: Record<string, CampoEsquema>
  required?: string[]
}

export interface Esquema extends CampoEsquema {
  type: 'object'
  properties: Record<string, CampoEsquema>
}

export type ResultadoEsquema = { ok: true; esquema: Esquema } | { ok: false; errores: string[] }

export const MAX_LONGITUD_ESQUEMA = 20_000
const MAX_PROFUNDIDAD = 4
const MAX_CAMPOS = 100
const TIPOS: TipoCampo[] = ['string', 'number', 'integer', 'boolean', 'array', 'object']
const TIPOS_DE_LISTA = ['string', 'number', 'integer', 'object']
const FORMATOS = ['date', 'date-time', 'email']
const EMAIL = /^[^@\s]+@[^@\s]+\.[^@\s]+$/

const esObjeto = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v)
const unir = (ruta: string, nombre: string, separador = '.') => (ruta === '' ? nombre : ruta + separador + nombre)
const donde = (ruta: string) => (ruta === '' ? '' : `${ruta}: `)

// ------------------------------------------------------------------------------------------------ the schema

/** Parses a schema and checks that it is one this platform can render and enforce. */
export function leerEsquema(texto: string): ResultadoEsquema {
  if (texto.trim() === '') return { ok: false, errores: ['El esquema está vacío.'] }
  if (texto.length > MAX_LONGITUD_ESQUEMA) {
    return { ok: false, errores: [`El esquema es demasiado largo (máximo ${MAX_LONGITUD_ESQUEMA} caracteres).`] }
  }

  let raiz: unknown
  try {
    raiz = JSON.parse(texto)
  } catch (err) {
    return { ok: false, errores: [`El esquema no es un JSON válido: ${err instanceof Error ? err.message : 'error de sintaxis'}`] }
  }

  if (!esObjeto(raiz)) return { ok: false, errores: ['El esquema debe ser un objeto JSON.'] }

  const errores: string[] = []
  if ('type' in raiz && raiz.type !== 'object') errores.push('El esquema raíz debe ser de tipo "object".')

  const contador = { campos: 0 }
  validarPropiedades(raiz, '', 1, errores, contador)

  return errores.length > 0 ? { ok: false, errores } : { ok: true, esquema: raiz as unknown as Esquema }
}

function validarPropiedades(objeto: Record<string, unknown>, ruta: string, profundidad: number, errores: string[], contador: { campos: number }) {
  const propiedades = objeto.properties
  if (!esObjeto(propiedades)) {
    errores.push(`${donde(ruta)}falta "properties" con los campos.`)
    return
  }

  const nombres = Object.keys(propiedades)
  for (const nombre of nombres) {
    contador.campos++
    if (contador.campos > MAX_CAMPOS) {
      if (contador.campos === MAX_CAMPOS + 1) errores.push(`Demasiados campos (máximo ${MAX_CAMPOS}).`)
      continue
    }

    if (nombre.trim() === '') {
      errores.push(`${donde(ruta)}hay un campo sin nombre.`)
      continue
    }

    validarCampo(propiedades[nombre], unir(ruta, nombre), profundidad, errores, contador)
  }

  if (nombres.length === 0) errores.push(`${donde(ruta)}"properties" no tiene ningún campo.`)

  if ('required' in objeto) {
    if (!Array.isArray(objeto.required)) {
      errores.push(`${donde(ruta)}"required" debe ser una lista de nombres de campo.`)
      return
    }

    for (const requerido of objeto.required) {
      if (typeof requerido !== 'string' || !nombres.includes(requerido)) {
        errores.push(`${donde(ruta)}"required" nombra un campo que no existe: ${JSON.stringify(requerido)}.`)
      }
    }
  }
}

function validarCampo(campo: unknown, ruta: string, profundidad: number, errores: string[], contador: { campos: number }) {
  if (!esObjeto(campo)) {
    errores.push(`${ruta}: debe ser un objeto con al menos "type".`)
    return
  }

  const erroresAntes = errores.length

  if (typeof campo.type !== 'string') {
    errores.push(`${ruta}: falta "type".`)
    return
  }

  const tipo = campo.type
  if (!TIPOS.includes(tipo as TipoCampo)) {
    errores.push(`${ruta}: el tipo "${tipo}" no está soportado (usa ${TIPOS.join(', ')}).`)
    return
  }

  for (const clave of ['title', 'description']) {
    if (clave in campo && typeof campo[clave] !== 'string') errores.push(`${ruta}: "${clave}" debe ser texto.`)
  }

  if (tipo === 'string') validarTexto(campo, ruta, errores)
  else if (tipo === 'number' || tipo === 'integer') validarNumero(campo, ruta, tipo, errores)
  else if (tipo === 'array') validarLista(campo, ruta, profundidad, errores, contador)
  else if (tipo === 'object') {
    if (profundidad >= MAX_PROFUNDIDAD) errores.push(`${ruta}: demasiado anidado (máximo ${MAX_PROFUNDIDAD} niveles).`)
    else validarPropiedades(campo, ruta, profundidad + 1, errores, contador)
  }

  // A default that the field itself would reject is a trap for whoever fills in the form.
  if ('default' in campo && errores.length === erroresAntes) {
    const erroresDefecto: string[] = []
    validarValor(campo as unknown as CampoEsquema, campo.default, ruta, false, erroresDefecto)
    for (const error of erroresDefecto) errores.push(`${ruta}: el valor por defecto no es válido. ${error}`)
  }
}

function enteroNoNegativo(campo: Record<string, unknown>, clave: string, ruta: string, errores: string[]): number | undefined {
  if (!(clave in campo)) return undefined
  const valor = campo[clave]
  if (typeof valor !== 'number' || !Number.isInteger(valor) || valor < 0) {
    errores.push(`${ruta}: "${clave}" debe ser un número entero mayor o igual que 0.`)
    return undefined
  }
  return valor
}

function validarTexto(campo: Record<string, unknown>, ruta: string, errores: string[]) {
  const minimo = enteroNoNegativo(campo, 'minLength', ruta, errores)
  const maximo = enteroNoNegativo(campo, 'maxLength', ruta, errores)
  if (minimo !== undefined && maximo !== undefined && minimo > maximo) errores.push(`${ruta}: "minLength" no puede ser mayor que "maxLength".`)

  if ('pattern' in campo) {
    if (typeof campo.pattern !== 'string') errores.push(`${ruta}: "pattern" debe ser texto.`)
    else {
      try {
        new RegExp(campo.pattern)
      } catch {
        errores.push(`${ruta}: "pattern" no es una expresión regular válida.`)
      }
    }
  }

  if ('format' in campo && !(typeof campo.format === 'string' && FORMATOS.includes(campo.format))) {
    errores.push(`${ruta}: "format" no está soportado (usa ${FORMATOS.join(', ')}).`)
  }

  if ('x-widget' in campo && campo['x-widget'] !== 'textarea') errores.push(`${ruta}: "x-widget" solo admite "textarea".`)

  validarEnumeracion(campo, ruta, 'string', errores)
}

function validarNumero(campo: Record<string, unknown>, ruta: string, tipo: string, errores: string[]) {
  let minimo: number | undefined
  let maximo: number | undefined
  for (const clave of ['minimum', 'maximum']) {
    if (!(clave in campo)) continue
    const valor = campo[clave]
    if (typeof valor !== 'number' || !Number.isFinite(valor)) {
      errores.push(`${ruta}: "${clave}" debe ser un número.`)
      continue
    }
    if (clave === 'minimum') minimo = valor
    else maximo = valor
  }

  if (minimo !== undefined && maximo !== undefined && minimo > maximo) errores.push(`${ruta}: "minimum" no puede ser mayor que "maximum".`)
  if ('x-suffix' in campo && typeof campo['x-suffix'] !== 'string') errores.push(`${ruta}: "x-suffix" debe ser texto.`)

  validarEnumeracion(campo, ruta, tipo, errores)
}

function validarEnumeracion(campo: Record<string, unknown>, ruta: string, tipo: string, errores: string[]) {
  if (!('enum' in campo)) return

  const opciones = campo.enum
  if (!Array.isArray(opciones) || opciones.length === 0) {
    errores.push(`${ruta}: "enum" debe ser una lista con al menos una opción.`)
    return
  }

  for (const opcion of opciones) {
    const correcta = tipo === 'string' ? typeof opcion === 'string' : typeof opcion === 'number' && (tipo === 'number' || Number.isInteger(opcion))
    if (!correcta) errores.push(`${ruta}: la opción ${JSON.stringify(opcion)} de "enum" no es de tipo ${tipo}.`)
  }

  if ('enumNames' in campo) {
    const nombres = campo.enumNames
    if (!Array.isArray(nombres) || nombres.length !== opciones.length || nombres.some((n) => typeof n !== 'string')) {
      errores.push(`${ruta}: "enumNames" debe ser una lista de textos, uno por cada opción de "enum".`)
    }
  }
}

function validarLista(campo: Record<string, unknown>, ruta: string, profundidad: number, errores: string[], contador: { campos: number }) {
  const minimo = enteroNoNegativo(campo, 'minItems', ruta, errores)
  const maximo = enteroNoNegativo(campo, 'maxItems', ruta, errores)
  if (minimo !== undefined && maximo !== undefined && minimo > maximo) errores.push(`${ruta}: "minItems" no puede ser mayor que "maxItems".`)

  const elementos = campo.items
  if (!esObjeto(elementos)) {
    errores.push(`${ruta}: falta "items" con el tipo de cada elemento de la lista.`)
    return
  }

  if (typeof elementos.type !== 'string' || !TIPOS_DE_LISTA.includes(elementos.type)) {
    errores.push(`${ruta}: los elementos de la lista deben ser de tipo ${TIPOS_DE_LISTA.join(', ')}.`)
    return
  }

  validarCampo(elementos, ruta + '[]', profundidad, errores, contador)
}

// ------------------------------------------------------------------------------------------------ the data

export interface ErrorDeDato {
  /** Dotted path of the field: `lineas.1.sku`. Empty for the document as a whole. */
  ruta: string
  /** What is wrong, without the field's name (the form puts it under the field). */
  mensaje: string
  /** The same message with the field's label in front, as the server words it. */
  completo: string
}

/** Checks data (parsed JSON) against a schema the way the server does. */
export function validarDatos(esquema: Esquema, datos: unknown): ErrorDeDato[] {
  const errores: ErrorDeDato[] = []
  if (!esObjeto(datos)) {
    return [{ ruta: '', mensaje: 'Los datos deben ser un objeto JSON.', completo: 'Los datos deben ser un objeto JSON.' }]
  }

  validarObjeto(esquema, datos, '', '', errores)
  return errores
}

function validarObjeto(esquema: CampoEsquema, datos: Record<string, unknown>, ruta: string, etiqueta: string, errores: ErrorDeDato[]) {
  const requeridos = new Set(esquema.required ?? [])
  for (const [nombre, campo] of Object.entries(esquema.properties ?? {})) {
    validarValorEnRuta(campo, datos[nombre], unir(ruta, nombre), unir(etiqueta, campo.title?.trim() || nombre, ' › '), requeridos.has(nombre), errores)
  }
}

function numeroTexto(n: number) {
  return String(n)
}

function validarValor(campo: CampoEsquema, valor: unknown, etiqueta: string, obligatorio: boolean, mensajes: string[]) {
  const errores: ErrorDeDato[] = []
  validarValorEnRuta(campo, valor, '', etiqueta, obligatorio, errores)
  for (const e of errores) mensajes.push(e.completo)
}

function validarValorEnRuta(campo: CampoEsquema, valor: unknown, ruta: string, etiqueta: string, obligatorio: boolean, errores: ErrorDeDato[]) {
  const anotar = (mensaje: string): void => {
    errores.push({ ruta, mensaje, completo: `${etiqueta}: ${mensaje}` })
  }

  if (valor === undefined || valor === null || valor === '') {
    if (obligatorio) anotar('es obligatorio.')
    return
  }

  switch (campo.type) {
    case 'string': {
      if (typeof valor !== 'string') return anotar('debe ser texto.')
      validarTextoValor(campo, valor, anotar)
      break
    }

    case 'number':
    case 'integer': {
      if (typeof valor !== 'number' || !Number.isFinite(valor)) return anotar(`debe ser un número${campo.type === 'integer' ? ' entero' : ''}.`)
      if (campo.type === 'integer' && !Number.isInteger(valor)) return anotar('debe ser un número entero.')
      if (campo.minimum !== undefined && valor < campo.minimum) anotar(`debe ser como mínimo ${numeroTexto(campo.minimum)}.`)
      if (campo.maximum !== undefined && valor > campo.maximum) anotar(`debe ser como máximo ${numeroTexto(campo.maximum)}.`)
      if (campo.enum && !campo.enum.includes(valor)) anotar(`debe ser uno de: ${campo.enum.join(', ')}.`)
      break
    }

    case 'boolean':
      if (typeof valor !== 'boolean') anotar('debe ser verdadero o falso.')
      break

    case 'array': {
      if (!Array.isArray(valor)) return anotar('debe ser una lista.')
      if (valor.length === 0 && obligatorio) return anotar('es obligatorio.')
      if (campo.minItems !== undefined && valor.length < campo.minItems) anotar(`necesita al menos ${campo.minItems} elementos.`)
      if (campo.maxItems !== undefined && valor.length > campo.maxItems) anotar(`admite como máximo ${campo.maxItems} elementos.`)
      valor.forEach((elemento, i) => {
        // An element has to be there: a null or empty entry in a list is a hole, not "not filled in".
        validarValorEnRuta(campo.items!, elemento, unir(ruta, String(i)), `${etiqueta} #${i + 1}`, true, errores)
      })
      break
    }

    case 'object':
      if (!esObjeto(valor)) return anotar('debe ser un objeto.')
      validarObjeto(campo, valor, ruta, etiqueta, errores)
      break
  }
}

function validarTextoValor(campo: CampoEsquema, texto: string, anotar: (mensaje: string) => void) {
  if (campo.minLength !== undefined && texto.length < campo.minLength) anotar(`debe tener al menos ${campo.minLength} caracteres.`)
  if (campo.maxLength !== undefined && texto.length > campo.maxLength) anotar(`debe tener como máximo ${campo.maxLength} caracteres.`)
  if (campo.pattern !== undefined && !new RegExp(campo.pattern).test(texto)) anotar('no tiene el formato esperado.')

  if (campo.format === 'date' && !esFechaValida(texto)) anotar('debe ser una fecha válida (AAAA-MM-DD).')
  if (campo.format === 'date-time' && Number.isNaN(Date.parse(texto))) anotar('debe ser una fecha y hora válidas.')
  if (campo.format === 'email' && !EMAIL.test(texto)) anotar('debe ser un correo electrónico válido.')

  if (campo.enum && !campo.enum.includes(texto)) anotar(`debe ser uno de: ${campo.enum.join(', ')}.`)
}

function esFechaValida(texto: string): boolean {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(texto)
  if (!m) return false
  const [anio, mes, dia] = [Number(m[1]), Number(m[2]), Number(m[3])]
  const fecha = new Date(Date.UTC(anio, mes - 1, dia))
  return fecha.getUTCFullYear() === anio && fecha.getUTCMonth() === mes - 1 && fecha.getUTCDate() === dia
}

// ------------------------------------------------------------------------------------------------ form state <-> data

/**
 * What the form holds while it is being filled in. It is the data's own shape except that numbers are kept as the
 * text typed (so "1." or "-" survive while typing) and every field exists, empty if need be.
 */
export type EstadoCampo = string | boolean | EstadoCampo[] | { [campo: string]: EstadoCampo }
export type EstadoFormulario = { [campo: string]: EstadoCampo }

/** The empty form, with each field's default if it has one. */
export function estadoInicial(esquema: Esquema): EstadoFormulario {
  return aEstado(esquema, {})
}

/** Turns data (parsed JSON) into form state; a field that is missing takes its default. */
export function aEstado(esquema: CampoEsquema, datos: unknown): EstadoFormulario {
  const origen = esObjeto(datos) ? datos : {}
  const estado: EstadoFormulario = {}
  for (const [nombre, campo] of Object.entries(esquema.properties ?? {})) {
    estado[nombre] = campoAEstado(campo, nombre in origen ? origen[nombre] : campo.default)
  }
  return estado
}

/** A new, empty entry for a list of this field (with its default if it has one). */
export const estadoVacio = (campo: CampoEsquema): EstadoCampo => campoAEstado(campo, campo.default)

function campoAEstado(campo: CampoEsquema, valor: unknown): EstadoCampo {
  switch (campo.type) {
    case 'boolean':
      return valor === true
    case 'number':
    case 'integer':
      return typeof valor === 'number' ? String(valor) : typeof valor === 'string' ? valor : ''
    case 'string':
      return typeof valor === 'string' ? valor : ''
    case 'array':
      return Array.isArray(valor) ? valor.map((v) => campoAEstado(campo.items!, v)) : []
    case 'object':
      return aEstado(campo, valor)
  }
}

/**
 * Turns form state into the data to send. Empty fields are left out (a blank string, a blank number, an empty list
 * and an object with nothing in it) so optional fields do not travel as noise; booleans always travel, as true or
 * false. Keys of `base` that the schema does not describe are kept as they were — editing a document in a form must
 * not silently drop what the form cannot show.
 */
export function aDatos(esquema: CampoEsquema, estado: EstadoFormulario, base?: unknown): Record<string, unknown> {
  const datos: Record<string, unknown> = {}
  const conocidos = new Set(Object.keys(esquema.properties ?? {}))
  if (esObjeto(base)) {
    for (const [clave, valor] of Object.entries(base)) {
      if (!conocidos.has(clave)) datos[clave] = valor
    }
  }

  for (const [nombre, campo] of Object.entries(esquema.properties ?? {})) {
    const valor = campoADato(campo, estado[nombre])
    if (valor !== undefined) datos[nombre] = valor
  }
  return datos
}

function campoADato(campo: CampoEsquema, estado: EstadoCampo | undefined): unknown {
  switch (campo.type) {
    case 'boolean':
      return estado === true
    case 'string':
      return typeof estado === 'string' && estado !== '' ? estado : undefined
    case 'number':
    case 'integer': {
      if (typeof estado !== 'string' || estado.trim() === '') return undefined
      const numero = Number(estado.replace(',', '.'))
      // Not a number yet ("-", "1e"): keep the text so validation can say so instead of dropping it silently.
      return Number.isFinite(numero) ? numero : estado
    }
    case 'array': {
      if (!Array.isArray(estado)) return undefined
      const elementos = estado
        .map((e) => campoADato(campo.items!, e as EstadoCampo))
        // A blank row in a list is just a row not typed in yet.
        .filter((e) => e !== undefined)
      return elementos.length > 0 ? elementos : undefined
    }
    case 'object': {
      if (!esObjeto(estado)) return undefined
      const hijo = aDatos(campo, estado as EstadoFormulario)
      return Object.keys(hijo).length > 0 ? hijo : undefined
    }
  }
}

// ------------------------------------------------------------------------------------------------ from an example

/** Builds a schema from a sample document: the type of each value becomes the type of its field. A starting point to
 *  edit, not a finished form (nothing is required, no limits). */
export function inferirEsquema(ejemplo: unknown): Esquema | null {
  if (!esObjeto(ejemplo) || Object.keys(ejemplo).length === 0) return null
  return { type: 'object', properties: Object.fromEntries(Object.entries(ejemplo).map(([k, v]) => [k, inferirCampo(v, k)])) }
}

function inferirCampo(valor: unknown, nombre: string): CampoEsquema {
  const titulo = nombre.replace(/[_-]+/g, ' ').replace(/([a-z])([A-Z])/g, '$1 $2').trim()
  const campo = (c: CampoEsquema): CampoEsquema => ({ title: titulo.charAt(0).toUpperCase() + titulo.slice(1), ...c })

  if (typeof valor === 'boolean') return campo({ type: 'boolean' })
  // Always `number`: an example of 15 must not forbid 15.5 later, and a whole number is a valid number anyway.
  if (typeof valor === 'number') return campo({ type: 'number' })
  if (Array.isArray(valor)) {
    const primero = valor.find((v) => v !== null && v !== undefined)
    const items: CampoEsquema =
      esObjeto(primero) ? { type: 'object', properties: Object.fromEntries(Object.entries(primero).map(([k, v]) => [k, inferirCampo(v, k)])) }
      : typeof primero === 'number' ? { type: 'number' }
      : { type: 'string' }
    return campo({ type: 'array', items })
  }
  if (esObjeto(valor)) {
    return campo({ type: 'object', properties: Object.fromEntries(Object.entries(valor).map(([k, v]) => [k, inferirCampo(v, k)])) })
  }

  const texto = typeof valor === 'string' ? valor : ''
  if (/^\d{4}-\d{2}-\d{2}$/.test(texto)) return campo({ type: 'string', format: 'date' })
  if (EMAIL.test(texto)) return campo({ type: 'string', format: 'email' })
  return campo({ type: 'string', ...(texto.length > 80 || texto.includes('\n') ? { 'x-widget': 'textarea' as const } : {}) })
}

/** How many fields (at any depth) the schema describes: for a summary in a list. */
export function contarCampos(campo: CampoEsquema): number {
  if (campo.type === 'array') return campo.items ? contarCampos(campo.items) : 0
  if (campo.type !== 'object') return 1
  return Object.values(campo.properties ?? {}).reduce((total, hijo) => total + (hijo.type === 'object' ? contarCampos(hijo) : 1), 0)
}
