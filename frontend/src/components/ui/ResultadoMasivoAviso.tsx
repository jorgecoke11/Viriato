import { CircleCheck, TriangleAlert, X } from 'lucide-react'
import type { ResultadoMasivo } from '../../lib/accionesMasivas'

/**
 * The report of a bulk action, shown once it has run: what it did, and — folded, to look at when wanted — why each item that
 * was left alone was. Green when everything went through, amber when something was left out. The same for every action.
 */
export function ResultadoMasivoAviso({
  mensaje,
  resultado,
  alCerrar,
}: {
  /** What the action did, in its own words ("Se cancelaron 3 casos"). */
  mensaje: string
  resultado: ResultadoMasivo
  alCerrar: () => void
}) {
  const sinDetalle = resultado.omitidosSinDetalle ?? 0
  const totalOmitidos = resultado.omitidos.length + sinDetalle
  const conOmitidos = totalOmitidos > 0

  return (
    <div
      role="status"
      className={`flex items-start gap-3 rounded-xl border px-4 py-3 ${conOmitidos ? 'border-amber-200/70 bg-amber-50/60' : 'border-green-200/70 bg-green-50/60'}`}
    >
      {conOmitidos ? (
        <TriangleAlert size={20} aria-hidden="true" className="mt-0.5 shrink-0 text-amber-600" />
      ) : (
        <CircleCheck size={20} aria-hidden="true" className="mt-0.5 shrink-0 text-green-600" />
      )}
      <div className="min-w-0 flex-1 text-sm">
        <p className="font-semibold text-gray-900">
          {mensaje}
          {conOmitidos && `; ${totalOmitidos} se dejaron como estaban`}.
        </p>
        {conOmitidos && (
          <details className="mt-1 text-gray-700">
            <summary className="cursor-pointer text-xs font-medium text-gray-600 hover:text-gray-900">Ver por qué</summary>
            <ul className="mt-1.5 flex max-h-40 flex-col gap-1 overflow-auto text-xs">
              {resultado.omitidos.map((o) => (
                <li key={o.id}>
                  <span className="font-mono text-gray-500">{o.id.slice(0, 8)}</span> · {o.motivo}
                </li>
              ))}
              {sinDetalle > 0 && <li className="text-gray-500">…y {sinDetalle} más.</li>}
            </ul>
          </details>
        )}
      </div>
      <button type="button" aria-label="Cerrar el aviso" className="shrink-0 rounded-md p-1 text-gray-500 hover:bg-gray-100" onClick={alCerrar}>
        <X size={16} aria-hidden="true" />
      </button>
    </div>
  )
}
