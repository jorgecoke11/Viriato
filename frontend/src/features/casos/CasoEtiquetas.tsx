import { CircleCheck, CircleDashed } from 'lucide-react'
import { DatoRotulado } from '../../components/ui/DatoRotulado'
import { EtiquetaDeCategoria } from '../../components/ui/EtiquetaDeCategoria'
import { CasoEstadoBadge } from './CasoEstadoBadge'

// A Caso is described by three different things, and every screen that shows them must keep them apart:
// - its *situación*: what it is doing right now (running, queued, paused, over) — decided by the platform;
// - its *estado de negocio*: where it stands in the process (“Extrayendo precios”) — decided by the process;
// - its *tipo de caso*: what kind of case it is (“Balay”) — fixed when it is created.
// Each has its own look (the situación a badge with a status dot, the other two with an icon) and its own caption.

const AYUDA_SITUACION = 'Qué está haciendo el caso ahora mismo: ejecutándose, en cola, pausado o terminado.'
const AYUDA_ESTADO_DE_NEGOCIO = 'Dónde va el caso dentro de su proceso. Lo marca el propio proceso.'
const AYUDA_TIPO_DE_CASO = 'La clase de caso que es. Se fija al crearlo.'

/** The estado a process puts a case in. One that ends the case is solid green with a tick; one the case is only passing through is
 *  an outline, dashed and blue — so the two are different in shape and weight, not only in colour. */
export function EstadoDeNegocioBadge({ display, esFinal = false }: { display: string; esFinal?: boolean }) {
  const Icono = esFinal ? CircleCheck : CircleDashed
  return (
    <span
      title={`${AYUDA_ESTADO_DE_NEGOCIO} ${esFinal ? 'Este estado cierra el caso.' : 'El caso sigue en curso.'}`}
      className={`inline-flex items-center gap-1 rounded-md px-2 py-0.5 text-xs whitespace-nowrap ${
        esFinal
          ? 'border border-green-300 bg-green-100 font-semibold text-green-800'
          : 'border border-dashed border-blue-300 bg-blue-50 font-medium text-blue-700'
      }`}
    >
      <Icono size={12} aria-hidden="true" />
      {display}
    </span>
  )
}

export function TipoDeCasoBadge({ nombre }: { nombre: string }) {
  return <EtiquetaDeCategoria nombre={nombre} title={AYUDA_TIPO_DE_CASO} />
}

/** The three descriptions of a Caso side by side, each under its caption. A missing one is left out. */
export function CasoEtiquetas({
  estado,
  estadoNegocio,
  estadoNegocioFinal = false,
  tipoCaso,
}: {
  estado: string
  estadoNegocio: string | null
  estadoNegocioFinal?: boolean
  tipoCaso: string | null
}) {
  return (
    <div className="flex flex-wrap gap-x-7 gap-y-3">
      <DatoRotulado rotulo="Situación" ayuda={AYUDA_SITUACION}>
        <CasoEstadoBadge estado={estado} />
      </DatoRotulado>
      {estadoNegocio && (
        <DatoRotulado rotulo="Estado de negocio" ayuda={AYUDA_ESTADO_DE_NEGOCIO}>
          <EstadoDeNegocioBadge display={estadoNegocio} esFinal={estadoNegocioFinal} />
        </DatoRotulado>
      )}
      {tipoCaso && (
        <DatoRotulado rotulo="Tipo de caso" ayuda={AYUDA_TIPO_DE_CASO}>
          <TipoDeCasoBadge nombre={tipoCaso} />
        </DatoRotulado>
      )}
    </div>
  )
}
