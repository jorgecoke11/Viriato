// Parameters are plain text, but some hold a JSON document (e.g. the list of products a robot walks). These helpers
// decide when a value should be treated — and shown — as JSON; the stored text itself is never rewritten.

/** A value "looks like" JSON when it starts like an object or an array; plain numbers/words stay plain text. */
export function pareceJson(valor: string): boolean {
  const inicio = valor.trimStart()[0]
  return inicio === '{' || inicio === '['
}

export type JsonValor = { ok: true; valor: unknown } | { ok: false; error: string }

export function leerJson(valor: string): JsonValor {
  try {
    return { ok: true, valor: JSON.parse(valor) }
  } catch (err) {
    return { ok: false, error: err instanceof Error ? err.message : 'JSON no válido.' }
  }
}

/** For form validation: a message only when the value looks like JSON but does not parse. */
export function errorDeJson(valor: string): string | null {
  if (!pareceJson(valor)) return null
  const resultado = leerJson(valor)
  return resultado.ok ? null : `JSON no válido: ${resultado.error}`
}

export function formatearJson(valor: unknown): string {
  return JSON.stringify(valor, null, 2)
}

const plural = (n: number, singular: string, pluralForma: string) => `${n} ${n === 1 ? singular : pluralForma}`

/** One short line for a table cell, e.g. `productos: 26 elementos`. */
export function resumenJson(valor: unknown): string {
  if (Array.isArray(valor)) return plural(valor.length, 'elemento', 'elementos')

  if (valor !== null && typeof valor === 'object') {
    const claves = Object.entries(valor)
    // The common shape — one key holding a list — reads better as "that list has N items".
    if (claves.length === 1 && Array.isArray(claves[0][1])) {
      return `${claves[0][0]}: ${plural(claves[0][1].length, 'elemento', 'elementos')}`
    }
    return plural(claves.length, 'clave', 'claves')
  }

  return 'valor'
}
