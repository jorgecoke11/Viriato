import type { ItemOmitido } from '../../lib/accionesMasivas'

/** What the server says about the zip it built (header `X-Viriato-Resumen`). */
export interface ResumenDeZip {
  documentos: number
  casosConDocumentos: number
  /** How many cases were left out; `omitidos` names the first ones only. */
  omitidosTotal: number
  omitidos: ItemOmitido[]
}

export const CABECERA_DE_RESUMEN = 'X-Viriato-Resumen'

const VACIO: ResumenDeZip = { documentos: 0, casosConDocumentos: 0, omitidosTotal: 0, omitidos: [] }

/** Reads the header: base64 of a UTF-8 JSON (so that accents and quotes travel in a header). A missing or damaged one is just "nothing to say". */
export function leerResumenDeZip(cabecera: string | null): ResumenDeZip {
  if (!cabecera) return VACIO
  try {
    const bytes = Uint8Array.from(atob(cabecera), (c) => c.charCodeAt(0))
    const json = JSON.parse(new TextDecoder().decode(bytes)) as Partial<ResumenDeZip>
    return {
      documentos: Number(json.documentos) || 0,
      casosConDocumentos: Number(json.casosConDocumentos) || 0,
      omitidosTotal: Number(json.omitidosTotal) || 0,
      omitidos: Array.isArray(json.omitidos)
        ? json.omitidos.filter((o): o is ItemOmitido => typeof o?.id === 'string' && typeof o?.motivo === 'string')
        : [],
    }
  } catch {
    return VACIO
  }
}

/** The name of one of the zips of a download: with a part number only when the selection needed several. */
export function nombreDelZip(nombreDelServidor: string | null, parte: number, partes: number): string {
  const base = (nombreDelServidor ?? 'documentos.zip').replace(/\.zip$/i, '')
  return partes > 1 ? `${base}-parte-${parte}-de-${partes}.zip` : `${base}.zip`
}

/** The file name the server proposed in `Content-Disposition`, if it did. */
export function nombreDeContentDisposition(cabecera: string | null): string | null {
  if (!cabecera) return null
  const codificado = /filename\*=(?:UTF-8'')?([^;]+)/i.exec(cabecera)
  if (codificado) {
    try {
      return decodeURIComponent(codificado[1].trim().replace(/^"|"$/g, ''))
    } catch {
      // fall through to the plain form
    }
  }
  const simple = /filename="?([^";]+)"?/i.exec(cabecera)
  return simple ? simple[1].trim() : null
}
