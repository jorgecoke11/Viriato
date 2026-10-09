import { ListOrdered, Repeat } from 'lucide-react'
import type { PoliticaDespacho } from '../api'

const OPCIONES: { valor: PoliticaDespacho; titulo: string; texto: string; icono: typeof Repeat }[] = [
  {
    valor: 'Prioridad',
    titulo: 'Por prioridad',
    texto: 'Va primero el servicio más arriba de la lista que tenga trabajo, siempre. Los de abajo esperan a que los de arriba se queden sin nada.',
    icono: ListOrdered,
  },
  {
    valor: 'Turnos',
    titulo: 'Por turnos',
    texto: 'Se turnan: tras uno, va el siguiente de la lista que tenga trabajo, y al llegar al final se vuelve a empezar. Ninguno se queda sin su turno.',
    icono: Repeat,
  },
]

/** How a machine chooses among services that all have work waiting. */
export function PoliticaSelector({
  value,
  onChange,
  disabled = false,
}: {
  value: PoliticaDespacho
  onChange: (valor: PoliticaDespacho) => void
  disabled?: boolean
}) {
  return (
    <div role="radiogroup" aria-label="Cómo se elige entre servicios" className="grid gap-2 sm:grid-cols-2">
      {OPCIONES.map(({ valor, titulo, texto, icono: Icono }) => {
        const activa = value === valor
        return (
          <button
            key={valor}
            type="button"
            role="radio"
            aria-checked={activa}
            disabled={disabled}
            className={`flex flex-col gap-1 rounded-xl border p-3 text-left ${
              activa ? 'border-indigo-500 bg-indigo-50 ring-2 ring-indigo-500/20' : 'border-gray-200 bg-surface hover:border-gray-300 hover:bg-gray-50'
            }`}
            onClick={() => onChange(valor)}
          >
            <span className={`flex items-center gap-2 text-sm font-semibold ${activa ? 'text-indigo-700' : 'text-gray-900'}`}>
              <Icono size={16} aria-hidden="true" />
              {titulo}
            </span>
            <span className="text-xs text-gray-600">{texto}</span>
          </button>
        )
      })}
    </div>
  )
}
