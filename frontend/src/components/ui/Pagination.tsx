import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Button } from './Button'

/** "26–50 de 112" with the buttons to move between pages. It says nothing when everything fits on one. */
export function Pagination({
  pagina,
  tamano,
  total,
  alCambiar,
}: {
  /** The current page, from 1. */
  pagina: number
  tamano: number
  total: number
  alCambiar: (pagina: number) => void
}) {
  if (total <= tamano) return null

  const desde = (pagina - 1) * tamano + 1
  const hasta = Math.min(pagina * tamano, total)

  return (
    <nav aria-label="Paginación" className="flex items-center justify-between gap-3 text-xs text-gray-500">
      <span className="num">
        {desde}–{hasta} de {total}
      </span>
      <span className="flex items-center gap-1">
        <Button variant="secondary" size="sm" disabled={pagina <= 1} onClick={() => alCambiar(pagina - 1)}>
          <ChevronLeft size={14} aria-hidden="true" />
          Anterior
        </Button>
        <Button variant="secondary" size="sm" disabled={hasta >= total} onClick={() => alCambiar(pagina + 1)}>
          Siguiente
          <ChevronRight size={14} aria-hidden="true" />
        </Button>
      </span>
    </nav>
  )
}
