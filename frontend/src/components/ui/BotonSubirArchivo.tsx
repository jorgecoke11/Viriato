import { Upload } from 'lucide-react'
import type { ChangeEvent } from 'react'

interface Props {
  /** What the button says; while `subiendo`, «Subiendo…». */
  texto: string
  /** Receives the file chosen. The input is emptied first, so choosing the same file again fires again. */
  alElegir: (archivo: File) => void
  subiendo?: boolean
  disabled?: boolean
  /** File types offered by the picker, as in `<input accept>`, e.g. `.zip,application/zip`. */
  accept?: string
  className?: string
}

/** A primary button that opens the file picker and hands back the one chosen. */
export function BotonSubirArchivo({ texto, alElegir, subiendo = false, disabled = false, accept, className = '' }: Props) {
  const bloqueado = subiendo || disabled

  function alCambiar(event: ChangeEvent<HTMLInputElement>) {
    const archivo = event.target.files?.[0]
    event.target.value = ''
    if (archivo) alElegir(archivo)
  }

  return (
    <label
      aria-disabled={bloqueado}
      className={`inline-flex w-fit cursor-pointer items-center gap-2 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:brightness-110 aria-disabled:pointer-events-none aria-disabled:opacity-50 ${className}`}
    >
      <Upload size={15} aria-hidden="true" />
      {subiendo ? 'Subiendo…' : texto}
      <input type="file" accept={accept} className="hidden" onChange={alCambiar} disabled={bloqueado} />
    </label>
  )
}
