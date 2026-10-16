import { clasesDeCategoria, inicialDeCategoria } from '../../lib/categorias'

/** A kind of thing as a quiet pill: its letter in a small square, then its name (see `lib/categorias`). No colour of its own. */
export function EtiquetaDeCategoria({ nombre, title, size = 'md' }: { nombre: string; title?: string; size?: 'md' | 'lg' }) {
  const clases = clasesDeCategoria(nombre)
  return (
    <span
      title={title}
      className={`inline-flex max-w-full items-center rounded-md font-medium whitespace-nowrap ${size === 'lg' ? 'gap-2 py-1 pr-3 pl-1 text-sm' : 'gap-1.5 py-0.5 pr-2 pl-0.5 text-xs'} ${clases.ficha}`}
    >
      <span aria-hidden="true" className={`flex shrink-0 items-center justify-center rounded bg-surface/70 font-bold ${size === 'lg' ? 'h-6 w-6 text-xs' : 'h-4 w-4 text-[10px]'}`}>
        {inicialDeCategoria(nombre)}
      </span>
      <span className="truncate">{nombre}</span>
    </span>
  )
}
