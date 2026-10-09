/** "Buenos días", "Buenas tardes" or "Buenas noches" for the hour of the day (the person's own clock). */
export function saludoSegunHora(fecha: Date = new Date()): string {
  const hora = fecha.getHours()
  if (hora >= 6 && hora < 14) return 'Buenos días'
  if (hora >= 14 && hora < 21) return 'Buenas tardes'
  return 'Buenas noches'
}

/** "Jueves, 8 de octubre" in Spanish, for the line above the greeting (only the first letter in capitals). */
export function fechaLarga(fecha: Date = new Date()): string {
  const texto = new Intl.DateTimeFormat('es-ES', { weekday: 'long', day: 'numeric', month: 'long' }).format(fecha)
  return texto.charAt(0).toLocaleUpperCase('es') + texto.slice(1)
}

/** The first name of a display name ("Jorge López" → "Jorge"), or an empty string when there is none. */
export function primerNombre(nombre: string | null | undefined): string {
  return (nombre ?? '').trim().split(/\s+/)[0] ?? ''
}
