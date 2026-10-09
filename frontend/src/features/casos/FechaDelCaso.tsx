import { formatFecha, formatFechaCompleta, haceCuanto } from './fechas'

/** A date as every list of Casos shows it: short, with how long ago it was underneath (or another line of the caller's), and the complete date as a tooltip. */
export function FechaDelCaso({ iso, debajo }: { iso: string; debajo?: string }) {
  return (
    <div title={formatFechaCompleta(iso)}>
      <div className="num whitespace-nowrap text-gray-900">{formatFecha(iso)}</div>
      <div className="num mt-0.5 text-xs whitespace-nowrap text-gray-500">{debajo ?? haceCuanto(iso)}</div>
    </div>
  )
}
