import { Ban, Download, RotateCcw } from 'lucide-react'
import { ejecutarEnTandas, type AccionMasiva } from '../../lib/accionesMasivas'
import * as casosApi from './api'
import { guardarBlob } from './fileUtils'
import { nombreDelZip } from './resumenDeZip'

const enTandas = (accion: string) => (ids: string[]) =>
  ejecutarEnTandas(ids, casosApi.MAXIMO_POR_PETICION, (tanda) => casosApi.ejecutarAccionMasiva(accion, tanda))

const casos = (n: number) => (n === 1 ? '1 caso' : `${n} casos`)

/**
 * Downloads the documents of the selection: one zip per request (a long selection goes in several, numbered), each handed to the
 * browser as it arrives. A case with no documents is not an error: it is reported as left out.
 */
const descargarDocumentos = (ids: string[]) => {
  const partes = Math.ceil(ids.length / casosApi.MAXIMO_POR_PETICION)
  let parte = 0
  return ejecutarEnTandas(ids, casosApi.MAXIMO_POR_PETICION, async (tanda) => {
    parte += 1
    const { blob, nombre, resumen } = await casosApi.descargarDocumentosDeCasos(tanda)
    if (blob) guardarBlob(blob, nombreDelZip(nombre, parte, partes))
    return {
      procesados: resumen.casosConDocumentos,
      omitidos: resumen.omitidos,
      omitidosSinDetalle: Math.max(0, resumen.omitidosTotal - resumen.omitidos.length),
      datos: { documentos: resumen.documentos },
    }
  })
}

/** Refreshed after any action on Casos: the lists, the dashboard's figures and the machines' queues. */
const INVALIDAR = ['casos', 'casos-resumen', 'cola-equipo'] as const

/**
 * What can be done to many Casos at once. The screen (AccionesMasivasPage) draws a button for each entry; a new action is a new
 * entry — with its confirmation and the request it makes — and a class on the server that answers to the same name
 * (see IAccionMasivaSobreCaso). Nothing else changes.
 */
export const accionesMasivasDeCasos: AccionMasiva[] = [
  {
    id: 'cancelar',
    etiqueta: (n) => `Cancelar ${casos(n)}`,
    icono: Ban,
    peligrosa: true,
    permiso: 'casos.cancelar',
    confirmacion: {
      titulo: (n) => (n === 1 ? '¿Cancelar este caso?' : `¿Cancelar ${n} casos?`),
      mensaje:
        'Se cancelarán igual que desde su página: no seguirán adelante, no se podrán reanudar y lo que estén ejecutando los robots se descartará. No se puede deshacer.',
      etiquetaDeConfirmar: 'Cancelar casos',
      etiquetaPendiente: 'Cancelando…',
      escribirDesde: { cantidad: 10, texto: 'CANCELAR' },
    },
    ejecutar: enTandas('cancelar'),
    mensajeDeExito: (r) => (r.procesados === 1 ? 'Se canceló 1 caso' : `Se cancelaron ${r.procesados} casos`),
    invalidar: INVALIDAR,
  },
  {
    id: 'reprocesar',
    etiqueta: (n) => `Reprocesar ${casos(n)}`,
    icono: RotateCcw,
    permiso: 'casos.manage',
    confirmacion: {
      titulo: (n) => (n === 1 ? '¿Reprocesar este caso?' : `¿Reprocesar ${n} casos?`),
      mensaje:
        'Se vuelve a lanzar el paso que falló en cada uno, igual que con «Reprocesar» desde su página: vuelve a la cola de su robot con la prioridad que tenía. Solo se reprocesan los casos fallidos; el resto se deja como está.',
      etiquetaDeConfirmar: 'Reprocesar casos',
      etiquetaPendiente: 'Reprocesando…',
    },
    ejecutar: enTandas('reprocesar'),
    mensajeDeExito: (r) => (r.procesados === 1 ? 'Se reprocesó 1 caso' : `Se reprocesaron ${r.procesados} casos`),
    invalidar: INVALIDAR,
  },
  {
    id: 'descargar-documentos',
    etiqueta: (n) => `Descargar documentos de ${casos(n)}`,
    icono: Download,
    permiso: 'casos.descargar',
    // A download changes nothing and is easy to repeat: no confirmation.
    confirmacion: null,
    ejecutar: descargarDocumentos,
    mensajeDeExito: (r) => {
      const documentos = r.datos?.documentos ?? 0
      if (documentos === 0) return 'No había documentos que descargar'
      return `Se descargaron ${documentos === 1 ? '1 documento' : `${documentos} documentos`} de ${casos(r.procesados)}`
    },
    invalidar: [],
  },
]
