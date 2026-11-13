import { UploadCloud } from 'lucide-react'
import { useRef, useState, type DragEvent, type ReactNode } from 'react'
import { BotonSubirArchivo } from './BotonSubirArchivo'

interface Props {
  /** What the button says; while `subiendo`, «Subiendo…». */
  texto: string
  /** What it says above the button, e.g. «Arrastra aquí el .zip del robot». */
  indicacion: ReactNode
  alElegir: (archivo: File) => void
  /** Called instead of `alElegir` when the dropped file does not match `extensiones`. */
  alRechazar?: (archivo: File) => void
  /** Lower-case extensions accepted on drop, e.g. `['.zip']`. Empty accepts anything. */
  extensiones?: readonly string[]
  accept?: string
  subiendo?: boolean
  disabled?: boolean
}

/** A dashed box to drop one file on, with the usual button inside for whoever prefers the picker. */
export function ZonaDeSubida({ texto, indicacion, alElegir, alRechazar, extensiones = [], accept, subiendo = false, disabled = false }: Props) {
  // dragenter/dragleave also fire for every child crossed: count them so the highlight does not flicker.
  const entradas = useRef(0)
  const [encima, setEncima] = useState(false)
  const bloqueado = subiendo || disabled

  const conArchivos = (event: DragEvent) => event.dataTransfer.types.includes('Files')

  function alEntrar(event: DragEvent) {
    if (!conArchivos(event)) return
    event.preventDefault()
    entradas.current += 1
    if (!bloqueado) setEncima(true)
  }

  function alSalir(event: DragEvent) {
    if (!conArchivos(event)) return
    entradas.current = Math.max(0, entradas.current - 1)
    if (entradas.current === 0) setEncima(false)
  }

  function alPasar(event: DragEvent) {
    if (!conArchivos(event)) return
    event.preventDefault()
    event.dataTransfer.dropEffect = bloqueado ? 'none' : 'copy'
  }

  function alSoltar(event: DragEvent) {
    if (!conArchivos(event)) return
    event.preventDefault()
    entradas.current = 0
    setEncima(false)
    const archivo = event.dataTransfer.files[0]
    if (!archivo || bloqueado) return
    const nombre = archivo.name.toLowerCase()
    if (extensiones.length > 0 && !extensiones.some((ext) => nombre.endsWith(ext))) alRechazar?.(archivo)
    else alElegir(archivo)
  }

  return (
    <div
      onDragEnter={alEntrar}
      onDragLeave={alSalir}
      onDragOver={alPasar}
      onDrop={alSoltar}
      className={`flex flex-col items-center gap-3 rounded-xl border-2 border-dashed px-4 py-6 text-center transition-colors ${
        encima ? 'border-indigo-500 bg-indigo-50' : 'border-gray-300 bg-gray-50/60'
      }`}
    >
      <UploadCloud size={28} aria-hidden="true" className={encima ? 'text-indigo-600' : 'text-gray-400'} />
      <p className="text-sm text-gray-600">{encima ? 'Suéltalo para subirlo' : indicacion}</p>
      <BotonSubirArchivo texto={texto} accept={accept} subiendo={subiendo} disabled={disabled} alElegir={alElegir} />
    </div>
  )
}
