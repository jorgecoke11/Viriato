/** One thing that can be picked from a list. */
export interface Opcion {
  id: string
  etiqueta: string
}

export const sinAcentos = (texto: string) => texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase()

/** The options whose label contains the text, ignoring case and accents ("extraccion" finds "Extracción"). All of them if the text is empty. */
export function filtrarOpciones<T extends Opcion>(opciones: readonly T[], texto: string): T[] {
  const buscado = sinAcentos(texto.trim())
  return buscado === '' ? [...opciones] : opciones.filter((o) => sinAcentos(o.etiqueta).includes(buscado))
}

/** What a selection reads as on a button: "Todos los procesos (5)", "3 de 100 procesos", "Ningún proceso". */
export function resumirSeleccion(elegidas: number, total: number, plural: string, singular: string): string {
  if (total === 0) return `Sin ${plural}`
  if (elegidas === 0) return `Ningún ${singular}`
  if (elegidas === total) return `Todos los ${plural} (${total})`
  return `${elegidas} de ${total} ${plural}`
}

/** The selection with all of `ids` ticked (`marcar`) or all of them unticked. The rest of the selection is left as it was. */
export function marcarTodas(seleccion: ReadonlySet<string>, ids: readonly string[], marcar: boolean): Set<string> {
  const nueva = new Set(seleccion)
  for (const id of ids) {
    if (marcar) nueva.add(id)
    else nueva.delete(id)
  }
  return nueva
}

/**
 * A choice kept by what is left OUT, not by what is in: the person starts with everything and takes away what they do not want,
 * so something new that appears later shows up on its own instead of being silently missing. These two turn the list of what is
 * left out into the selection and back.
 */
export function elegidosSegunExcluidos(ids: readonly string[], excluidos: readonly string[]): Set<string> {
  const fuera = new Set(excluidos)
  return new Set(ids.filter((id) => !fuera.has(id)))
}

/** The new list of what is left out once the person has chosen `elegidos` among `ids`. What was left out and is no longer among
 *  `ids` (a process they lost access to) is kept as it was, in case it comes back. */
export function excluidosTrasElegir(ids: readonly string[], elegidos: ReadonlySet<string>, excluidosPrevios: readonly string[]): string[] {
  const actuales = new Set(ids)
  return [...excluidosPrevios.filter((id) => !actuales.has(id)), ...ids.filter((id) => !elegidos.has(id))]
}
