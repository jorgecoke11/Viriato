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
        {/* The bar is always full width and slides in from the left: transform, unlike width, does not lay the page out again
            on every frame, and its rounded end keeps its shape. */}
        <span
          className="block h-full rounded-full bg-indigo-500 transition-transform duration-300 ease-[cubic-bezier(0.16,1,0.3,1)] motion-reduce:transition-none"
          style={{ transform: `translateX(${valor - 100}%)` }}
        />
      </span>
      {mostrarCifra && <span className="num w-9 shrink-0 text-right text-xs font-medium text-gray-700">{valor} %</span>}
    </span>
  )
}
