export type ValorDeDato =
  | { tipo: 'texto'; texto: string }
  | { tipo: 'numero'; texto: string }
  | { tipo: 'booleano'; texto: string }
  | { tipo: 'vacio' }
  /** An object or a list: shown folded, with what it holds said in a few words and the JSON behind it. */
  | { tipo: 'compuesto'; resumen: string; json: string }

export interface EntradaDeDato {
  clave: string
  etiqueta: string
  valor: ValorDeDato
}

export type DatosDelCaso =
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

function valorDe(valor: unknown): ValorDeDato {
  if (valor === null || valor === undefined || valor === '') return { tipo: 'vacio' }
  if (typeof valor === 'string') return { tipo: 'texto', texto: valor }
  if (typeof valor === 'number') return { tipo: 'numero', texto: String(valor) }
  if (typeof valor === 'boolean') return { tipo: 'booleano', texto: valor ? 'Sí' : 'No' }
  const json = JSON.stringify(valor, null, 2)
  if (Array.isArray(valor)) return { tipo: 'compuesto', resumen: valor.length === 1 ? '1 elemento' : `${valor.length} elementos`, json }
  const campos = Object.keys(valor as object).length
  return { tipo: 'compuesto', resumen: campos === 1 ? '1 campo' : `${campos} campos`, json }
}

/** Reads the business data of a Caso (the JSON text the API gives) into what the screen shows. */
export function leerDatosDelCaso(datosJson: string | null): DatosDelCaso {
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
