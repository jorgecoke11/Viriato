import { useNavigate } from 'react-router-dom'
import type { CasoListItemDto } from './api'
import { CasoEstadoBadge } from './CasoEstadoBadge'
import { formatFecha, formatFechaCompleta, haceCuanto, ultimoResultado } from './fechas'

// Not finished is not always "running": a paused Caso or one waiting for a person is not moving on its own.
const sinFinalizar: Record<string, string> = { Pausado: 'en pausa', EsperandoRevisionHumana: 'esperando revisión' }

const cabecera = 'px-4 py-2.5 text-left text-xs font-medium tracking-wide whitespace-nowrap text-gray-500 uppercase'

function Fecha({ iso, debajo }: { iso: string; debajo?: string }) {
  return (
    <div title={formatFechaCompleta(iso)}>
      <div className="num whitespace-nowrap text-gray-900">{formatFecha(iso)}</div>
      <div className="num mt-0.5 text-xs whitespace-nowrap text-gray-500">{debajo ?? haceCuanto(iso)}</div>
    </div>
  )
}

/**
 * The list of Casos, in the one shape used wherever Casos are listed. Each column answers one question, and the dates
 * carry how long ago they were underneath so they never have to be subtracted in your head:
 * - Caso: its title, and its type under it.
 * - Estado: how it stands technically (badge) and what the business says about it (text under it).
 * - Creado: when it came in.
 * - Último resultado: when it last finished — the latest, after any reprocess. Still moving: when it last did anything.
 */
export function CasosTabla({ casos, className = '' }: { casos: CasoListItemDto[]; className?: string }) {
  const navigate = useNavigate()

  return (
    <div className={`overflow-x-auto ${className}`}>
      <table className="w-full min-w-[640px] border-collapse text-sm">
        <thead className="border-b border-gray-200 bg-gray-50/70">
          <tr>
            <th scope="col" className={cabecera}>Caso</th>
            <th scope="col" className={cabecera}>Estado</th>
            <th scope="col" className={cabecera}>Creado</th>
            <th scope="col" className={cabecera}>Último resultado</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {casos.map((caso) => {
            const { finalizadoAt, actividadAt } = ultimoResultado(caso)
            return (
              <tr
                key={caso.id}
                className="cursor-pointer align-top hover:bg-gray-50"
                onClick={() => navigate(`/casos/${caso.id}`)}
              >
                <td className="max-w-[18rem] px-4 py-3">
                  <button
                    type="button"
                    className="block max-w-full truncate text-left font-medium text-gray-900 hover:text-indigo-600 focus-visible:underline"
                    title={caso.titulo}
                    onClick={(e) => {
                      e.stopPropagation()
                      navigate(`/casos/${caso.id}`)
                    }}
                  >
                    {caso.titulo}
                  </button>
                  {caso.tipoCaso && <div className="mt-0.5 truncate text-xs text-gray-500">{caso.tipoCaso}</div>}
                </td>
                <td className="px-4 py-3">
                  <CasoEstadoBadge estado={caso.estado} />
                  {caso.estadoNegocio && <div className="mt-1 text-xs text-gray-500">{caso.estadoNegocio.display}</div>}
                </td>
                <td className="px-4 py-3">
                  <Fecha iso={caso.createdAt} />
                </td>
                <td className="px-4 py-3">
                  {finalizadoAt ? (
                    <Fecha iso={finalizadoAt} />
                  ) : (
                    <Fecha iso={actividadAt} debajo={`${sinFinalizar[caso.estado] ?? 'en curso'} · ${haceCuanto(actividadAt)}`} />
                  )}
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
