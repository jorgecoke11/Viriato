import { useEffect, useMemo, useRef, useState } from 'react'
import { Button } from '../ui/Button'
import { SchemaForm } from './SchemaForm'
import { aDatos, aEstado, contarCampos, inferirEsquema, leerEsquema, validarDatos } from './esquema'
import type { EstadoFormulario } from './esquema'

// A starting point that shows the main ideas: a required number with a unit and limits, a switch, a choice and a list.
const PLANTILLA = `{
  "type": "object",
  "required": ["beneficio"],
  "properties": {
    "beneficio": { "type": "number", "title": "Beneficio", "minimum": 0, "maximum": 100, "x-suffix": "%" },
    "actualizar": { "type": "boolean", "title": "Actualizar precios", "default": false },
    "tipo": { "type": "string", "title": "Tipo", "enum": ["a", "b"], "enumNames": ["Opción A", "Opción B"] },
    "etiquetas": { "type": "array", "title": "Etiquetas", "items": { "type": "string" } }
  }
}`

interface EditorDeEsquemaProps {
  /** The schema text. */
  value: string
  onChange: (texto: string) => void
}

/**
 * Where a form is defined: the schema on the left, the form it produces on the right — filled in live, with the JSON it would send
 * underneath — so what is being built is always in front of whoever builds it. Instead of writing the schema from scratch it can be
 * generated from an example JSON (types are inferred from the values), or started from a template.
 */
export function EditorDeEsquema({ value, onChange }: EditorDeEsquemaProps) {
  const [ejemplo, setEjemplo] = useState('')
  const [mostrarEjemplo, setMostrarEjemplo] = useState(false)
  const [errorEjemplo, setErrorEjemplo] = useState<string | null>(null)
  const [estado, setEstado] = useState<EstadoFormulario>({})
  // The preview only scolds once someone has typed in it: an untouched, empty form is not an error.
  const [tocado, setTocado] = useState(false)
  const datosPrevios = useRef<Record<string, unknown>>({})

  const resultado = useMemo(() => (value.trim() === '' ? null : leerEsquema(value)), [value])
  const esquema = resultado?.ok ? resultado.esquema : null

  // When the schema changes under the preview, what was already typed in it is kept wherever it still fits.
  useEffect(() => {
    if (esquema) setEstado(aEstado(esquema, datosPrevios.current))
  }, [esquema])

  const datos = useMemo(() => (esquema ? aDatos(esquema, estado) : {}), [esquema, estado])
  const problemas = useMemo(() => (esquema ? validarDatos(esquema, datos) : []), [esquema, datos])
  const erroresPorRuta = useMemo(() => {
    const porRuta: Record<string, string> = {}
    for (const p of problemas) if (!(p.ruta in porRuta)) porRuta[p.ruta] = p.mensaje
    return porRuta
  }, [problemas])

  function generarDesdeEjemplo() {
    setErrorEjemplo(null)
    let valor: unknown
    try {
      valor = JSON.parse(ejemplo)
    } catch {
      setErrorEjemplo('El ejemplo no es un JSON válido.')
      return
    }
    const generado = inferirEsquema(valor)
    if (!generado) {
      setErrorEjemplo('El ejemplo debe ser un objeto JSON con al menos un campo, p. ej. { "beneficio": 15, "actualizar": true }.')
      return
    }
    onChange(JSON.stringify(generado, null, 2))
    setMostrarEjemplo(false)
  }

  function formatear() {
    try {
      onChange(JSON.stringify(JSON.parse(value), null, 2))
    } catch {
      /* the editor already shows why it is not valid */
    }
  }

  return (
    <div className="grid gap-5 md:grid-cols-2">
      <div className="flex flex-col gap-2">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <label htmlFor="esquema-json" className="text-sm font-medium text-gray-700">
            Esquema (JSON)
          </label>
          <div className="flex items-center gap-3 text-xs">
            <button type="button" className="text-indigo-600 hover:text-indigo-800" onClick={() => setMostrarEjemplo((v) => !v)}>
              Generar desde un ejemplo
            </button>
            <button type="button" className="text-indigo-600 hover:text-indigo-800" onClick={() => onChange(PLANTILLA)}>
              Plantilla
            </button>
            <button type="button" className="text-gray-500 hover:text-gray-800 disabled:opacity-40" disabled={!resultado?.ok} onClick={formatear}>
              Formatear
            </button>
          </div>
        </div>

        {mostrarEjemplo && (
          <div className="flex flex-col gap-2 rounded-lg border border-indigo-100 bg-indigo-50/40 p-3">
            <p className="text-xs text-gray-600">
              Pega un JSON de ejemplo con valores reales: el tipo de cada campo (número, texto, sí/no, lista…) se deduce de su valor. Después podrás
              ajustar títulos, obligatorios y límites.
            </p>
            <textarea
              rows={5}
              className="field font-mono text-xs"
              value={ejemplo}
              onChange={(e) => setEjemplo(e.target.value)}
              placeholder={'{\n  "beneficio": 15,\n  "actualizar": true,\n  "producto": "Lavadoras"\n}'}
              spellCheck={false}
            />
            {errorEjemplo && <p className="text-xs text-red-600">{errorEjemplo}</p>}
            <Button type="button" className="self-start" disabled={ejemplo.trim() === ''} onClick={generarDesdeEjemplo}>
              Generar esquema
            </Button>
          </div>
        )}

        <textarea
          id="esquema-json"
          rows={mostrarEjemplo ? 12 : 20}
          className="field font-mono text-xs"
          aria-invalid={resultado && !resultado.ok ? true : undefined}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          spellCheck={false}
          placeholder="Escribe el esquema, pega uno, o usa «Generar desde un ejemplo»."
        />

        {resultado?.ok && <p className="text-xs text-green-700">Esquema válido · {contarCampos(resultado.esquema)} campos</p>}
        {resultado && !resultado.ok && (
          <ul className="list-disc pl-5 text-xs text-red-600">
            {resultado.errores.map((e) => (
              <li key={e}>{e}</li>
            ))}
          </ul>
        )}
        <p className="text-xs text-gray-500">
          Tipos: <code>string</code>, <code>number</code>, <code>integer</code>, <code>boolean</code>, <code>array</code>, <code>object</code>. El orden de{' '}
          <code>properties</code> es el orden del formulario; <code>required</code> marca los obligatorios.
        </p>
      </div>

      <div className="flex flex-col gap-3">
        <span className="text-sm font-medium text-gray-700">Vista previa</span>
        {esquema ? (
          <>
            <div className="rounded-lg border border-gray-200 p-3">
              <SchemaForm
                esquema={esquema}
                estado={estado}
                onChange={(nuevo) => {
                  setEstado(nuevo)
                  setTocado(true)
                  datosPrevios.current = aDatos(esquema, nuevo)
                }}
                errores={tocado ? erroresPorRuta : {}}
              />
            </div>
            <div className="flex flex-col gap-1">
              <span className="text-xs font-medium text-gray-500">JSON que se enviaría</span>
              <pre className="max-h-48 overflow-auto whitespace-pre-wrap break-words rounded-lg bg-gray-50 p-3 font-mono text-xs text-gray-800">
                {Object.keys(datos).length > 0 ? JSON.stringify(datos, null, 2) : '{}'}
              </pre>
              {tocado && problemas.length > 0 && <p className="text-xs text-amber-700">Con estos valores el formulario avisaría: {problemas[0].completo}</p>}
            </div>
          </>
        ) : (
          <p className="rounded-lg border border-dashed border-gray-300 p-6 text-center text-sm text-gray-500">Cuando el esquema sea válido verás aquí el formulario.</p>
        )}
      </div>
    </div>
  )
}
