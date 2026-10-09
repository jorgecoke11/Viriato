import { clasesDeCategoria, inicialDeCategoria } from '../../lib/categorias'

/** A kind of thing as a quiet pill: its letter in a small square, then its name (see `lib/categorias`). No colour of its own. */
export function EtiquetaDeCategoria({ nombre, title }: { nombre: string; title?: string }) {
  const clases = clasesDeCategoria(nombre)
  return (
    <span
      title={title}
      className={`inline-flex max-w-full items-center gap-1.5 rounded-md py-0.5 pr-2 pl-0.5 text-xs font-medium whitespace-nowrap ${clases.ficha}`}
    >
      <span aria-hidden="true" className="flex h-4 w-4 shrink-0 items-center justify-center rounded bg-surface/70 text-[10px] font-bold">
        {inicialDeCategoria(nombre)}
      </span>
      <span className="truncate">{nombre}</span>
    </span>
  )
}
