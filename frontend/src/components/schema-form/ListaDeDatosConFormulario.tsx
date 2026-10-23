import { Plus, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Button } from '../ui/Button'
import { ControlSegmentado } from '../ui/ControlSegmentado'
import { IconButton } from '../ui/IconButton'
import { SchemaForm } from './SchemaForm'
import { erroresDeListaDeDatos, leerElementos } from './comprobar'
import { aDatos, aEstado, diferencia, leerEsquema, leerObjeto, mezclarDatos, validarDatos } from './esquema'
import type { Esquema, EstadoFormulario } from './esquema'

type Modo = 'uno' | 'varios' | 'json'

interface ListaDeDatosConFormularioProps {
  /** The schema of the form, or empty when there is none (the data is then free-form JSON). */
  esquemaJson: string | null | undefined
  /** What every document starts from (a creator's defaults, say), as a JSON object; only what differs from it is kept. */
  baseJson: string | null | undefined
  /** The JSON text: one object, a list of objects, or empty. */
  value: string
  onChange: (json: string) => void
  mostrarErrores: boolean
  etiqueta?: string
}

/**
 * One document, or a list of them, edited through the same form, each starting from a common base. What it reports is only what each
 * one changes from the base — so a later change to the base still reaches every field nobody touched. It is how a launcher's data are
 * written (one case, or one case per row) but it knows nothing about launchers.
 */
export function ListaDeDatosConFormulario({ esquemaJson, baseJson, value, onChange, mostrarErrores, etiqueta = 'Datos del caso' }: ListaDeDatosConFormularioProps) {
  const resultado = useMemo(() => (esquemaJson?.trim() ? leerEsquema(esquemaJson) : null), [esquemaJson])
  const esquema = resultado?.ok ? resultado.esquema : null
  const base = useMemo(() => leerObjeto(baseJson) ?? {}, [baseJson])

  const inicial = leerElementos(value)
  const [modo, setModo] = useState<Modo>(() => (!esquema || inicial === null ? 'json' : inicial.lista ? 'varios' : 'uno'))
  const [estados, setEstados] = useState<EstadoFormulario[]>(() =>
    esquema && inicial ? inicial.elementos.map((e) => aEstado(esquema, mezclarDatos(base, e))) : [],
  )
  const [aviso, setAviso] = useState<string | null>(null)

  function cambios(esquemaActual: Esquema, estado: EstadoFormulario, anterior: Record<string, unknown> | undefined) {
    return diferencia(base, aDatos(esquemaActual, estado, mezclarDatos(base, anterior ?? {})))
  }

  function emitir(nuevos: EstadoFormulario[], modoActual: Modo) {
    if (!esquema) return
    const anteriores = leerElementos(value)?.elementos ?? []
    const elementos = nuevos.map((estado, i) => cambios(esquema, estado, anteriores[i]))
    if (modoActual === 'uno') onChange(Object.keys(elementos[0] ?? {}).length > 0 ? JSON.stringify(elementos[0]) : '')
    else onChange(JSON.stringify(elementos))
  }

  function cambiarElemento(i: number, estado: EstadoFormulario) {
    const nuevos = estados.map((e, j) => (j === i ? estado : e))
    setEstados(nuevos)
    emitir(nuevos, modo)
  }

  function cambiarModo(nuevo: Modo) {
    setAviso(null)
    if (nuevo === 'json' || !esquema) {
      setModo('json')
      return
    }
    const leido = leerElementos(value)
    if (leido === null) {
      setAviso('Corrige el JSON antes de volver al formulario.')
      return
    }
    let nuevos = leido.elementos.map((e) => aEstado(esquema, mezclarDatos(base, e)))
    if (nuevo === 'uno' && nuevos.length > 1) {
      setAviso('Hay varios casos: quita filas hasta dejar uno para pasar a «Un caso».')
      return
    }
    if (nuevos.length === 0) nuevos = [aEstado(esquema, base)]
    setEstados(nuevos)
    setModo(nuevo)
    emitir(nuevos, nuevo)
  }

  const errores = useMemo(
    () => (mostrarErrores ? erroresDeListaDeDatos(esquemaJson, baseJson, value) : []),
    [mostrarErrores, esquemaJson, baseJson, value],
  )

  function erroresDe(i: number): Record<string, string> {
    if (!esquema || !mostrarErrores) return {}
    const anterior = leerElementos(value)?.elementos[i]
    const porRuta: Record<string, string> = {}
    for (const e of validarDatos(esquema, mezclarDatos(base, cambios(esquema, estados[i], anterior)))) {
      if (!(e.ruta in porRuta)) porRuta[e.ruta] = e.mensaje
    }
    return porRuta
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="text-sm font-medium text-gray-700">{etiqueta}</span>
        {esquema && (
          <ControlSegmentado
            etiqueta="Cuántos casos y cómo escribirlos"
            valor={modo}
            onChange={cambiarModo}
            opciones={[
              { valor: 'uno', etiqueta: 'Un caso' },
              { valor: 'varios', etiqueta: 'Un caso por fila' },
              { valor: 'json', etiqueta: 'JSON' },
            ]}
          />
        )}
      </div>

      {modo === 'json' || !esquema ? (
        <>
          <textarea rows={6} className="field font-mono text-xs" value={value} onChange={(e) => onChange(e.target.value)} spellCheck={false} />
          {errores.length > 0 && (
            <ul className="list-disc pl-5 text-sm text-red-600">
              {errores.map((e) => (
                <li key={e}>{e}</li>
              ))}
            </ul>
          )}
        </>
      ) : modo === 'uno' ? (
        <SchemaForm esquema={esquema} estado={estados[0] ?? {}} onChange={(e) => cambiarElemento(0, e)} errores={erroresDe(0)} />
      ) : (
        <div className="flex flex-col gap-3">
          {estados.map((estado, i) => (
            <div key={i} className="rounded-lg border border-gray-200 p-3">
              <div className="mb-2 flex items-center justify-between">
                <span className="text-xs font-medium text-gray-500">Caso {i + 1}</span>
                <IconButton
                  label={`Quitar el caso ${i + 1}`}
                  variant="danger"
                  size="sm"
                  disabled={estados.length === 1}
                  onClick={() => {
                    const nuevos = estados.filter((_, j) => j !== i)
                    setEstados(nuevos)
                    // The removed row's own changes go with it: the rest are re-read from their state, not by position.
                    if (esquema) onChange(JSON.stringify(nuevos.map((e) => diferencia(base, aDatos(esquema, e, base)))))
                  }}
                >
                  <Trash2 size={16} />
                </IconButton>
              </div>
              <SchemaForm esquema={esquema} estado={estado} onChange={(e) => cambiarElemento(i, e)} errores={erroresDe(i)} />
            </div>
          ))}
          <Button
            type="button"
            variant="secondary"
            className="self-start"
            onClick={() => {
              const nuevos = [...estados, aEstado(esquema, base)]
              setEstados(nuevos)
              emitir(nuevos, 'varios')
            }}
          >
            <Plus size={16} /> Añadir caso
          </Button>
        </div>
      )}
      {aviso && <span className="text-sm text-red-600">{aviso}</span>}
    </div>
  )
}
