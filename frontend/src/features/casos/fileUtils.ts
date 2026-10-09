import { apiFetchBlob } from '../../lib/apiClient'
import { documentoContenidoPath } from './api'

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  const units = ['KB', 'MB', 'GB']
  let value = bytes / 1024
  let unitIndex = 0
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024
    unitIndex += 1
  }
  return `${value.toFixed(1)} ${units[unitIndex]}`
}

// Content sits behind Authorization, so a plain <a href> can't download it: fetch as a Blob and
// click a temporary link instead.
/** Hands a file the browser already has to the person, as a download. */
export function guardarBlob(blob: Blob, nombre: string): void {
  const url = URL.createObjectURL(blob)
  const link = window.document.createElement('a')
  link.href = url
  link.download = nombre
  link.click()
  URL.revokeObjectURL(url)
}

export async function descargarDocumento(documentoId: string, nombre: string): Promise<void> {
  const blob = await apiFetchBlob(documentoContenidoPath(documentoId))
  const url = URL.createObjectURL(blob)
  const link = window.document.createElement('a')
  link.href = url
  link.download = nombre
  link.click()
  URL.revokeObjectURL(url)
}
