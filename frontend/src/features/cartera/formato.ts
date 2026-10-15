const euros = new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' })
const dolares = new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'USD' })
const cantidades = new Intl.NumberFormat('es-ES', { maximumFractionDigits: 4 })
const porcentajes = new Intl.NumberFormat('es-ES', { maximumFractionDigits: 2 })

export const formatEuros = (valor: number): string => euros.format(valor)
export const formatDolares = (valor: number): string => dolares.format(valor)
export const formatCantidad = (valor: number): string => cantidades.format(valor)

/** A change with its sign always showing ("+1,20 €", "−0,50 €"), so a gain and a loss never look alike. */
export function formatConSigno(valor: number, formatear: (n: number) => string): string {
  if (valor === 0) return formatear(0)
  return `${valor > 0 ? '+' : '−'}${formatear(Math.abs(valor))}`
}

export const formatPorcentaje = (valor: number): string => `${formatConSigno(valor, (n) => porcentajes.format(n))} %`
