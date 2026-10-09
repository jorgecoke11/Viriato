import { Input } from '../../../components/ui/Input'
import { leerTope, type DespachoConfig } from './despachoConfig'
import { OrdenServicios, type ServicioEnOrden } from './OrdenServicios'
import { PoliticaSelector } from './PoliticaSelector'

interface DespachoConfigFormProps {
  value: DespachoConfig
  onChange: (value: DespachoConfig) => void
  /** Every service that can take a place in the order (the ones the machine runs, or all of them for a template). */
  candidatos: ServicioEnOrden[]
  disabled?: boolean
}

/** How many things at once, how to choose between services, and in what order: the same three decisions whether
 *  they are being set for one machine or saved in a template. */
export function DespachoConfigForm({ value, onChange, candidatos, disabled = false }: DespachoConfigFormProps) {
  const enOrden = new Set(value.orden.map((s) => s.id))
  const sinPosicion = candidatos.filter((c) => !enOrden.has(c.id))
  const topeInvalido = !leerTope(value.maxEjecucionesSimultaneas).valido

  return (
    <div className="flex flex-col gap-5">
      <Input
        label="Límite de ejecuciones a la vez (opcional)"
        name="maxEjecucionesSimultaneas"
        type="number"
        min={1}
        max={50}
        step={1}
        placeholder="Sin límite"
        value={value.maxEjecucionesSimultaneas}
        disabled={disabled}
        error={topeInvalido ? 'Un número entero entre 1 y 50, o vacío para no limitar.' : undefined}
        hint="Déjalo vacío: cuántas ejecuciones corren a la vez lo deciden las copias de cada robot que tengas en marcha (réplicas en el stack o procesos en Windows). Pon un número solo si quieres frenar la máquina; con 1, los robots van uno detrás de otro, lo más seguro si usan la pantalla o el mismo navegador."
        onChange={(e) => onChange({ ...value, maxEjecucionesSimultaneas: e.target.value })}
      />

      <div className="flex flex-col gap-2">
        <span className="text-sm font-medium text-gray-700">Cuando varios servicios tienen trabajo</span>
        <PoliticaSelector value={value.politica} disabled={disabled} onChange={(politica) => onChange({ ...value, politica })} />
      </div>

      <div className="flex flex-col gap-2">
        <span className="text-sm font-medium text-gray-700">Orden de los servicios</span>
        <OrdenServicios orden={value.orden} sinPosicion={sinPosicion} disabled={disabled} onChange={(orden) => onChange({ ...value, orden })} />
      </div>
    </div>
  )
}
