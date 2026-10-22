// Checks on data and schemas written as text — what the editors of this folder show, and what a form that holds one of them uses to
// decide whether it can be saved. The rules themselves are in esquema.ts (and, in step, on the server).
import { leerEsquema, leerObjeto, mezclarDatos, validarDatos } from './esquema'
import type { OpcionesDeValidacion } from './esquema'

export const JSON_INVALIDO = 'El JSON no es válido — revisa la sintaxis.'

const esObjeto = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v)

/** What is wrong with a schema text, or null when it is one this platform can draw and enforce. */
export function errorDeEsquema(texto: string): string | null {
  if (texto.trim() === '') return 'Escribe el esquema del formulario.'
  const resultado = leerEsquema(texto)
  return resultado.ok ? null : resultado.errores[0]
}

/** What is wrong with a document of data given as text, against a schema (or none, when any JSON object will do). */
export function erroresDeDatos(esquemaJson: string | null | undefined, texto: string, opciones: OpcionesDeValidacion = {}): string[] {
  const objeto = leerObjeto(texto)
  if (objeto === null) return [JSON_INVALIDO]
  const resultado = esquemaJson?.trim() ? leerEsquema(esquemaJson) : null
  return resultado?.ok ? validarDatos(resultado.esquema, objeto, opciones).map((e) => e.completo) : []
}

/** The elements of a value that may be one document (an object) or several (a non-empty list of objects); null when it is neither.
 *  Blank is one empty document. */
export function leerElementos(texto: string): { lista: boolean; elementos: Record<string, unknown>[] } | null {
  if (texto.trim() === '') return { lista: false, elementos: [{}] }
  try {
    const valor: unknown = JSON.parse(texto)
    if (esObjeto(valor)) return { lista: false, elementos: [valor] }
    if (Array.isArray(valor) && valor.length > 0 && valor.every(esObjeto)) return { lista: true, elementos: valor }
    return null
  } catch {
    return null
  }
}

/**
 * What is wrong with one-or-several documents given as text, each laid over `baseJson` and checked in full against the form (or
 * none: any JSON objects). A list's messages say which element ("Caso 2: …"), as the server does.
 */
export function erroresDeListaDeDatos(esquemaJson: string | null | undefined, baseJson: string | null | undefined, texto: string): string[] {
  const leido = leerElementos(texto)
  if (leido === null) return ['Tiene que ser un objeto JSON (un caso) o una lista de objetos (un caso por elemento).']
  const resultado = esquemaJson?.trim() ? leerEsquema(esquemaJson) : null
  if (!resultado?.ok) return []
  const base = leerObjeto(baseJson) ?? {}
  return leido.elementos.flatMap((elemento, i) =>
    validarDatos(resultado.esquema, mezclarDatos(base, elemento)).map((e) => (leido.lista ? `Caso ${i + 1}: ${e.completo}` : e.completo)),
  )
}
