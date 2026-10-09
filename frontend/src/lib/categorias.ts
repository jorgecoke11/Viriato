/**
 * How a *kind* of thing (the type of a case, a type of document) is drawn. On purpose it has no colour of its own: green, amber,
 * red and blue already mean something here (done, waiting, failed, running), so a kind is told apart by what it says — its name
 * and its letter in a quiet square — and the screen stays calm. Colour is left for what is happening, not for what something is.
 */

/** One letter to stand for it. */
export const inicialDeCategoria = (nombre: string): string => (nombre.trim().charAt(0) || '?').toLocaleUpperCase('es')

/** The classes of a kind, written out in full so Tailwind sees them. `ficha` is the little square with the letter. */
export const CLASES_DE_CATEGORIA = {
  ficha: 'bg-gray-100 text-gray-700',
}

/** What something with no kind ("Sin tipo") looks like: the same square, quieter, so it recedes next to the ones that have one. */
export const CLASES_SIN_CATEGORIA = {
  ficha: 'bg-gray-50 text-gray-400',
}

/** The look of a kind by name; `null` is "no kind". */
export const clasesDeCategoria = (nombre: string | null) => (nombre === null ? CLASES_SIN_CATEGORIA : CLASES_DE_CATEGORIA)
