/** "13/11/26, 9:05" — a moment in time, short, for a table cell. */
export const fechaHora = (iso: string) => new Date(iso).toLocaleString('es-ES', { dateStyle: 'short', timeStyle: 'short' })
