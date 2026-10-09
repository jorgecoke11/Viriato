import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useEffect, type ReactNode } from 'react'
import { IconButton } from './IconButton'
import { Modal } from './Modal'

export interface ElementoDeGaleria {
  id: string
  titulo: string
  /** A line under the title ("Paso «Extraer precios» · 11:52"). */
  detalle?: string
  contenido: ReactNode
}

/**
 * Looks at one element of a set at full size, with the arrows (on screen and on the keyboard) to go through the rest. It is
 * the viewer for a gallery of anything visual; what each element draws is the caller's.
 */
export function Lightbox({
  elementos,
  indice,
  alCambiar,
  alCerrar,
}: {
  elementos: readonly ElementoDeGaleria[]
  /** The element being looked at; null = closed. */
  indice: number | null
  alCambiar: (indice: number) => void
  alCerrar: () => void
}) {
  const abierto = indice !== null && elementos[indice] !== undefined
  const actual = abierto ? elementos[indice] : null
  const total = elementos.length

  useEffect(() => {
    if (!abierto) return
    const alPulsar = (e: KeyboardEvent) => {
      if (e.key === 'ArrowLeft' && indice > 0) alCambiar(indice - 1)
      if (e.key === 'ArrowRight' && indice < total - 1) alCambiar(indice + 1)
    }
    document.addEventListener('keydown', alPulsar)
    return () => document.removeEventListener('keydown', alPulsar)
  }, [abierto, indice, total, alCambiar])

  return (
    <Modal
      open={abierto}
      size="xl"
      onClose={alCerrar}
      title={
        <span className="flex flex-wrap items-baseline gap-x-2">
          <span>{actual?.titulo ?? ''}</span>
          {actual?.detalle && <span className="text-sm font-normal text-gray-500">{actual.detalle}</span>}
        </span>
      }
    >
      {actual && (
        <div className="relative">
          {actual.contenido}
          {total > 1 && indice !== null && (
            <>
              <span className="absolute top-1/2 left-0 -translate-y-1/2">
                <IconButton label="Anterior" variant="secondary" disabled={indice === 0} onClick={() => alCambiar(indice - 1)}>
                  <ChevronLeft size={20} />
                </IconButton>
              </span>
              <span className="absolute top-1/2 right-0 -translate-y-1/2">
                <IconButton label="Siguiente" variant="secondary" disabled={indice === total - 1} onClick={() => alCambiar(indice + 1)}>
                  <ChevronRight size={20} />
                </IconButton>
              </span>
              <p className="num mt-3 text-center text-xs text-gray-500">
                {indice + 1} de {total}
              </p>
            </>
          )}
        </div>
      )}
    </Modal>
  )
}
