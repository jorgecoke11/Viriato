/** What a number field holds, as the API wants it: empty is null ("what the version says"), anything else an integer. */
export function numeroOpcional(texto: string): number | null {
  const limpio = texto.trim()
  if (limpio === '') return null
  const n = Number(limpio)
  return Number.isInteger(n) ? n : Number.NaN
}

/** "12,4 MB", "830 KB": how big an uploaded robot is, for people. */
export function tamanoLegible(bytes: number): string {
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} KB`
  return `${(bytes / (1024 * 1024)).toLocaleString('es-ES', { maximumFractionDigits: 1 })} MB`
}
