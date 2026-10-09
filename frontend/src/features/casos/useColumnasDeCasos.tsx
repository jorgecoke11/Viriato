import { useNavigate } from 'react-router-dom'
import type { ColumnaDeTabla } from '../../components/ui/DataTable'
import type { CasoListItemDto } from './api'
import { BarraDeProgreso } from '../../components/ui/BarraDeProgreso'
import { BotonConLaVista } from './BotonConLaVista'
import { CasoEstadoBadge } from './CasoEstadoBadge'
import { EstadoDeNegocioBadge, TipoDeCasoBadge } from './CasoEtiquetas'
import { FechaDelCaso } from './FechaDelCaso'
import { haceCuanto, ultimoResultado } from './fechas'

// Not finished is not always "running": a Caso can wait in the queue, be paused or wait for a person.
const sinFinalizar: Record<string, string> = {
  Pausado: 'en pausa',
  EsperandoRevisionHumana: 'esperando revisión',
  Pendiente: 'en cola',
  EnProgreso: 'ejecutándose',
}

/**
 * The columns of any list of Casos, in the one shape used wherever Casos are listed. Each column answers one question, and the
 * dates carry how long ago they were underneath so they never have to be subtracted in your head:
 * - Caso: its title (it opens the Caso).
 * - Proceso (only when asked for): which process it belongs to.
 * - Tipo, Situación and Estado de negocio: three different things, so three columns — what kind of case it is, what it is doing
 *   now (badge with a status dot), and where the process says it stands.
 * - Creado: when it came in.
 * - Último resultado: when it last finished — the latest, after any reprocess. Still moving: when it last did anything.
 */
export function useColumnasDeCasos(procesos?: ReadonlyMap<string, string>): ColumnaDeTabla<CasoListItemDto>[] {
  const navigate = useNavigate()

  return [
    {
      clave: 'caso',
      titulo: 'Caso',
      className: 'max-w-[18rem]',
      celda: (caso) => (
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
      ),
    },
    ...(procesos
      ? [
          {
            clave: 'proceso',
            titulo: 'Proceso',
            className: 'max-w-[14rem]',
            celda: (caso: CasoListItemDto) => <span className="block truncate text-gray-700">{procesos.get(caso.flujoId) ?? '—'}</span>,
          },
        ]
      : []),
    {
      clave: 'tipo',
      titulo: 'Tipo',
      celda: (caso) => (caso.tipoCaso ? <TipoDeCasoBadge nombre={caso.tipoCaso} /> : <span className="text-gray-400">—</span>),
    },
    {
      clave: 'situacion',
      titulo: 'Situación',
      celda: (caso) => (
        <span className="flex flex-col gap-1">
          <span className="flex items-center gap-1.5">
            <CasoEstadoBadge estado={caso.estado} />
            {/* A robot is running it and its screen can be watched: the camera opens it live. */}
            {caso.enVivo && (
              <span onClick={(e) => e.stopPropagation()}>
                <BotonConLaVista casoId={caso.id} titulo={caso.titulo} />
              </span>
            )}
          </span>
          {caso.estado === 'EnProgreso' && caso.progresoPorcentaje !== null && caso.progresoPorcentaje !== undefined && (
            <BarraDeProgreso porcentaje={caso.progresoPorcentaje} etiqueta="Avance del paso en marcha" className="max-w-40" />
          )}
        </span>
      ),
    },
    {
      clave: 'estadoNegocio',
      titulo: 'Estado de negocio',
      celda: (caso) => (caso.estadoNegocio ? <EstadoDeNegocioBadge display={caso.estadoNegocio.display} esFinal={caso.estadoNegocio.esFinal} /> : <span className="text-gray-400">—</span>),
    },
    { clave: 'creado', titulo: 'Creado', celda: (caso) => <FechaDelCaso iso={caso.createdAt} /> },
    {
      clave: 'resultado',
      titulo: 'Último resultado',
      celda: (caso) => {
        const { finalizadoAt, actividadAt } = ultimoResultado(caso)
        return finalizadoAt ? (
          <FechaDelCaso iso={finalizadoAt} />
        ) : (
          <FechaDelCaso iso={actividadAt} debajo={`${sinFinalizar[caso.estado] ?? 'en curso'} · ${haceCuanto(actividadAt)}`} />
        )
      },
    },
  ]
}
