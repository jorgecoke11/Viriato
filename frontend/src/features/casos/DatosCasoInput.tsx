import { useEffect, useMemo, useState } from 'react'
import { SchemaForm } from '../../components/schema-form/SchemaForm'
import { aDatos, aEstado, leerEsquema, validarDatos } from '../../components/schema-form/esquema'
import type { Esquema, EstadoFormulario } from '../../components/schema-form/esquema'

type Modo = 'formulario' | 'json'

const JSON_INVALIDO = 'El JSON no es válido — revisa la sintaxis.'

function parsearObjeto(texto: string): Record<string, unknown> | null {
  if (texto.trim() === '') return {}
  try {
    const valor: unknown = JSON.parse(texto)
    return typeof valor === 'object' && valor !== null && !Array.isArray(valor) ? (valor as Record<string, unknown>) : null
  } catch {
    return null
  }
}

interface DatosCasoInputProps {
  /** The schema of the chosen case type, or empty when it has none (the data is then free-form JSON). */
  esquemaJson: string | null | undefined
  /** The JSON text the parent will send. */
  value: string
  /** The new JSON text and what is wrong with it (empty when it can be sent). Called once on mount too, so defaults count. */
  onChange: (json: string, errores: string[]) => void
  /** Whether to show what is wrong: the parent turns it on once the user has tried to submit. */
  mostrarErrores: boolean
}

/**
 * The business data of a Caso. If its type has a schema this is the form that schema describes — with a switch to
 * the raw JSON for whoever prefers it — and if not, a JSON box as it has always been. Either way the parent only
 * sees a JSON text and a list of problems. Give it `key={tipoCasoId}` so changing the type starts a fresh form.
 */
export function DatosCasoInput({ esquemaJson, value, onChange, mostrarErrores }: DatosCasoInputProps) {
  const resultado = useMemo(() => (esquemaJson?.trim() ? leerEsquema(esquemaJson) : null), [esquemaJson])
  const esquema = resultado?.ok ? resultado.esquema : null

  const [modo, setModo] = useState<Modo>('formulario')
  const [estado, setEstado] = useState<EstadoFormulario>(() => (esquema ? aEstado(esquema, parsearObjeto(value) ?? {}) : {}))
  const [aviso, setAviso] = useState<string | null>(null)

  function emitirDesdeFormulario(esquemaActual: Esquema, nuevo: EstadoFormulario) {
    const datos = aDatos(esquemaActual, nuevo, parsearObjeto(value))
    const errores = validarDatos(esquemaActual, datos).map((e) => e.completo)
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
    const objeto = parsearObjeto(texto)
    if (objeto === null) {
      onChange(texto, [JSON_INVALIDO])
      return
    }
    onChange(texto, esquema ? validarDatos(esquema, objeto).map((e) => e.completo) : [])
  }

  function irAFormulario() {
    const objeto = parsearObjeto(value)
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
    const datos = aDatos(esquema, estado, parsearObjeto(value))
    const porRuta: Record<string, string> = {}
    for (const error of validarDatos(esquema, datos)) {
      if (!(error.ruta in porRuta)) porRuta[error.ruta] = error.mensaje
    }
    return porRuta
  }, [esquema, estado, mostrarErrores, value])

  const jsonInvalido = value.trim() !== '' && parsearObjeto(value) === null
  const erroresJson = useMemo(() => {
    if (!mostrarErrores && !jsonInvalido) return []
    const objeto = parsearObjeto(value)
    if (objeto === null) return [JSON_INVALIDO]
    return esquema ? validarDatos(esquema, objeto).map((e) => e.completo) : []
  }, [esquema, jsonInvalido, mostrarErrores, value])

  const titulo = (
    <span className="text-sm font-medium text-gray-700">
      Datos de negocio {!esquema && <span className="font-normal text-gray-400">(opcional)</span>}
    </span>
  )

  if (!esquema) {
    return (
      <div className="flex flex-col gap-1">
        {titulo}
        {resultado && !resultado.ok && (
          <p className="text-xs text-amber-700">
            El formulario de este tipo de caso tiene un esquema que no se puede usar, así que se piden los datos como JSON.
          </p>
        )}
        <textarea
          rows={5}
          className="rounded-lg border border-gray-300 px-3 py-2 font-mono text-xs focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
          value={value}
          onChange={(e) => cambiarJson(e.target.value)}
          placeholder={'{\n  "cliente": "ACME Corp"\n}'}
        />
        <p className="text-xs text-gray-500">JSON con los datos iniciales del caso — puedes dejarlo vacío y añadirlo más tarde.</p>
        {jsonInvalido && <span className="text-sm text-red-600">{JSON_INVALIDO}</span>}
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-2">
      <div className="flex items-center justify-between">
        {titulo}
        <div className="inline-flex rounded-lg border border-gray-200 p-0.5 text-xs">
          {(['formulario', 'json'] as const).map((m) => (
            <button
              key={m}
              type="button"
              className={`rounded-md px-2 py-0.5 ${modo === m ? 'bg-indigo-50 font-medium text-indigo-700' : 'text-gray-500 hover:text-gray-800'}`}
              onClick={() => (m === 'json' ? (setModo('json'), setAviso(null)) : irAFormulario())}
            >
              {m === 'formulario' ? 'Formulario' : 'JSON'}
            </button>
          ))}
        </div>
      </div>

      {modo === 'formulario' ? (
        <SchemaForm esquema={esquema} estado={estado} onChange={cambiarFormulario} errores={erroresDelFormulario} />
      ) : (
        <>
          <textarea
            rows={8}
            className="rounded-lg border border-gray-300 px-3 py-2 font-mono text-xs focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
            value={value}
            onChange={(e) => cambiarJson(e.target.value)}
            spellCheck={false}
          />
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
