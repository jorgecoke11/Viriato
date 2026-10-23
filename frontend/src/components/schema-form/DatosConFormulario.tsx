import { useEffect, useMemo, useState } from 'react'
import { ControlSegmentado } from '../ui/ControlSegmentado'
import { SchemaForm } from './SchemaForm'
import { JSON_INVALIDO, erroresDeDatos } from './comprobar'
import { aDatos, aEstado, leerEsquema, leerObjeto, validarDatos } from './esquema'
import type { Esquema, EstadoFormulario } from './esquema'

type Modo = 'formulario' | 'json'

interface DatosConFormularioProps {
  /** The schema of the form, or empty when there is none (the data is then free-form JSON). */
  esquemaJson: string | null | undefined
  /** The JSON text the parent will send. */
  value: string
  /** The new JSON text and what is wrong with it (empty when it can be sent). Called once on mount too, so defaults count. */
  onChange: (json: string, errores: string[]) => void
  /** Whether to show what is wrong: the parent turns it on once the user has tried to submit. */
  mostrarErrores: boolean
  /** See `validarDatos`: for defaults, which may leave required fields empty. */
  parcial?: boolean
  etiqueta?: string
}

/**
 * One JSON document edited through its form — with a switch to the raw JSON for whoever prefers it — or, without a form, a JSON
 * box. Either way the parent only sees a JSON text and a list of problems. It knows nothing about what the document is for: a
 * case's data, a creator's defaults. Give it a `key` that changes with the form so a new form starts fresh.
 */
export function DatosConFormulario({ esquemaJson, value, onChange, mostrarErrores, parcial = false, etiqueta = 'Datos de negocio' }: DatosConFormularioProps) {
  const resultado = useMemo(() => (esquemaJson?.trim() ? leerEsquema(esquemaJson) : null), [esquemaJson])
  const esquema = resultado?.ok ? resultado.esquema : null
  const opciones = useMemo(() => ({ parcial }), [parcial])

  const [modo, setModo] = useState<Modo>('formulario')
  const [estado, setEstado] = useState<EstadoFormulario>(() => (esquema ? aEstado(esquema, leerObjeto(value) ?? {}) : {}))
  const [aviso, setAviso] = useState<string | null>(null)

  function emitirDesdeFormulario(esquemaActual: Esquema, nuevo: EstadoFormulario) {
    const datos = aDatos(esquemaActual, nuevo, leerObjeto(value))
    const errores = validarDatos(esquemaActual, datos, opciones).map((e) => e.completo)
    onChange(Object.keys(datos).length > 0 ? JSON.stringify(datos) : '', errores)
  }

  // The form starts with its defaults filled in; the parent must hear about them even if the user types nothing.
  useEffect(() => {
    if (esquema) emitirDesdeFormulario(esquema, estado)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  function cambiarFormulario(nuevo: EstadoFormulario) {
    setEstado(nuevo)
    setAviso(null)
    if (esquema) emitirDesdeFormulario(esquema, nuevo)
  }

  function cambiarJson(texto: string) {
    setAviso(null)
    onChange(texto, erroresDeDatos(esquemaJson, texto, opciones))
  }

  function cambiarModo(nuevo: Modo) {
    if (nuevo === 'json') {
      setModo('json')
      setAviso(null)
      return
    }
    const objeto = leerObjeto(value)
    if (objeto === null) {
      setAviso('Corrige el JSON antes de volver al formulario.')
      return
    }
    if (esquema) setEstado(aEstado(esquema, objeto))
    setAviso(null)
    setModo('formulario')
  }

  const erroresDelFormulario = useMemo(() => {
    if (!esquema || !mostrarErrores) return {}
    const datos = aDatos(esquema, estado, leerObjeto(value))
    const porRuta: Record<string, string> = {}
    for (const error of validarDatos(esquema, datos, opciones)) {
      if (!(error.ruta in porRuta)) porRuta[error.ruta] = error.mensaje
    }
    return porRuta
  }, [esquema, estado, mostrarErrores, value, opciones])

  const jsonInvalido = value.trim() !== '' && leerObjeto(value) === null
  const erroresJson = useMemo(
    () => (mostrarErrores || jsonInvalido ? erroresDeDatos(esquemaJson, value, opciones) : []),
    [esquemaJson, jsonInvalido, mostrarErrores, value, opciones],
  )

  const titulo = (
    <span className="text-sm font-medium text-gray-700">
      {etiqueta} {!esquema && <span className="font-normal text-gray-400">(opcional)</span>}
    </span>
  )

  if (!esquema) {
    return (
      <div className="flex flex-col gap-1">
        {titulo}
        {resultado && !resultado.ok && (
          <p className="text-xs text-amber-700">El formulario tiene un esquema que no se puede usar, así que se piden los datos como JSON.</p>
        )}
        <textarea
          rows={5}
          className="field font-mono text-xs"
          value={value}
          onChange={(e) => cambiarJson(e.target.value)}
          placeholder={'{\n  "cliente": "ACME Corp"\n}'}
          spellCheck={false}
        />
        <p className="text-xs text-gray-500">JSON — puedes dejarlo vacío.</p>
        {jsonInvalido && <span className="text-sm text-red-600">{JSON_INVALIDO}</span>}
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center justify-between">
        {titulo}
        <ControlSegmentado
          etiqueta="Cómo escribir los datos"
          valor={modo}
          onChange={cambiarModo}
          opciones={[
            { valor: 'formulario', etiqueta: 'Formulario' },
            { valor: 'json', etiqueta: 'JSON' },
          ]}
        />
      </div>

      {modo === 'formulario' ? (
        <SchemaForm esquema={esquema} estado={estado} onChange={cambiarFormulario} errores={erroresDelFormulario} />
      ) : (
        <>
          <textarea rows={8} className="field font-mono text-xs" value={value} onChange={(e) => cambiarJson(e.target.value)} spellCheck={false} />
          {erroresJson.length > 0 && (
            <ul className="list-disc pl-5 text-sm text-red-600">
              {erroresJson.map((error) => (
                <li key={error}>{error}</li>
              ))}
            </ul>
          )}
        </>
      )}
      {aviso && <span className="text-sm text-red-600">{aviso}</span>}
    </div>
  )
}
