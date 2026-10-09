import { clasesDeCategoria, inicialDeCategoria } from '../../lib/categorias'

/** The square with the letter that stands for a kind of thing (a type of case). `nombre` null is "no kind": a quiet grey one. The
 *  colour comes from the name (see `lib/categorias`), so the same kind has the same square everywhere. */
export function FichaDeCategoria({ nombre, tamano = 'md' }: { nombre: string | null; tamano?: 'sm' | 'md' }) {
  const clases = clasesDeCategoria(nombre)
  return (
    <span
      aria-hidden="true"
      className={`inline-flex shrink-0 items-center justify-center rounded-lg font-semibold ${clases.ficha} ${
        tamano === 'sm' ? 'h-5 w-5 text-[11px]' : 'h-8 w-8 text-sm'
      }`}
    >
      {nombre === null ? '–' : inicialDeCategoria(nombre)}
    </span>
  )
}
