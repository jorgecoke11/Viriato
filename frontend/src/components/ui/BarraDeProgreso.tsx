/** A slim bar for "how far along", 0 to 100, with the figure beside it. Anything outside the range is kept inside it. */
export function BarraDeProgreso({
  porcentaje,
  etiqueta,
  mostrarCifra = true,
  className = '',
}: {
  porcentaje: number
  /** What it measures, for a screen reader ("Avance del paso"). */
  etiqueta: string
  mostrarCifra?: boolean
  className?: string
}) {
  const valor = Math.max(0, Math.min(100, Math.round(porcentaje)))
  return (
    <span className={`flex items-center gap-2 ${className}`}>
      <span
        role="progressbar"
        aria-label={etiqueta}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={valor}
        className="h-1.5 min-w-12 flex-1 overflow-hidden rounded-full bg-gray-200"
      >
        <span className="block h-full rounded-full bg-indigo-500 transition-[width] duration-500" style={{ width: `${valor}%` }} />
      </span>
      {mostrarCifra && <span className="num w-9 shrink-0 text-right font-mono text-xs font-medium text-gray-700">{valor} %</span>}
    </span>
  )
}
