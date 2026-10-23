import { Braces, Check, ChevronDown, Copy, List } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { leerDatos, type EntradaDeDato, type TablaDeDatos } from '../../lib/datos'
import { useToast } from '../../lib/toast/useToast'
import { Card } from './Card'
import { IconButton } from './IconButton'

const VISIBLES = 8

function Tabla({ tabla }: { tabla: TablaDeDatos }) {
  return (
    <div className="mt-2 max-h-72 overflow-auto rounded-lg border border-gray-100">
      <table className="w-full text-left text-xs">
        <thead className="sticky top-0 bg-gray-50 text-gray-500">
          <tr>
            {tabla.columnas.map((c) => (
              <th key={c.clave} className="px-2.5 py-1.5 font-medium">
                {c.etiqueta}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {tabla.filas.map((fila, i) => (
            <tr key={i}>
              {fila.map((celda, j) => (
                <td key={j} className="num px-2.5 py-1 text-gray-800">
                  {celda === '' ? <span className="text-gray-300">—</span> : celda}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

function Valor({ entrada }: { entrada: EntradaDeDato }) {
  const { valor } = entrada
  switch (valor.tipo) {
    case 'vacio':
      return <span className="text-gray-400">—</span>
    case 'numero':
      return <span className="num font-mono text-gray-900">{valor.texto}</span>
    case 'booleano':
      return <span className="text-gray-900">{valor.texto}</span>
    case 'texto':
      return <span className="break-words text-gray-900">{valor.texto}</span>
    case 'compuesto':
      return (
        <details className="group">
          <summary className="inline-flex cursor-pointer list-none items-center gap-1 rounded-md bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-700 hover:bg-gray-200 [&::-webkit-details-marker]:hidden">
            {valor.resumen}
            <ChevronDown size={12} aria-hidden="true" className="transition-transform group-open:rotate-180" />
          </summary>
          {valor.tabla ? (
            <Tabla tabla={valor.tabla} />
          ) : (
            <pre className="mt-2 max-h-56 overflow-auto rounded-lg bg-gray-50 p-2.5 text-left text-xs text-gray-700">{valor.json}</pre>
          )}
        </details>
      )
  }
}

interface VisorDeDatosProps {
  titulo: ReactNode
  /** The JSON text to show, or null for nothing. */
  json: string | null
  /** What to say when there is nothing. */
  textoVacio: string
}

/**
 * A JSON document as a readable list instead of JSON: one row per field, with its label and its value. Lists and nested objects
 * stay folded until asked for, and a list of records (a history of readings, say) opens as a table. The JSON itself is one click
 * away, and can be copied. It knows nothing about what the document is: a case's data, what a robot keeps between cases…
 */
export function VisorDeDatos({ titulo, json, textoVacio }: VisorDeDatosProps) {
  const { showToast } = useToast()
  const datos = leerDatos(json)
  const [verJson, setVerJson] = useState(false)
  const [verTodos, setVerTodos] = useState(false)
  const [copiado, setCopiado] = useState(false)

  const jsonFormateado = (() => {
    if (json === null) return ''
    try {
      return JSON.stringify(JSON.parse(json), null, 2)
    } catch {
      return json
    }
  })()

  async function copiar() {
    try {
      await navigator.clipboard.writeText(jsonFormateado)
      setCopiado(true)
      setTimeout(() => setCopiado(false), 1500)
    } catch {
      showToast('error', 'No se pudo copiar.')
    }
  }

  const entradas = datos.tipo === 'entradas' ? datos.entradas : []
  const mostradas = verTodos ? entradas : entradas.slice(0, VISIBLES)

  return (
    <Card className="p-0">
      <div className="flex items-center justify-between gap-2 border-b border-gray-100 px-5 py-3">
        <h2 className="min-w-0 text-sm font-semibold text-gray-900">{titulo}</h2>
        {datos.tipo !== 'vacio' && (
          <span className="-my-2 flex items-center gap-0.5">
            <IconButton label={verJson ? 'Ver como lista' : 'Ver como JSON'} onClick={() => setVerJson((v) => !v)}>
              {verJson ? <List size={16} /> : <Braces size={16} />}
            </IconButton>
            <IconButton label={copiado ? 'Copiado' : 'Copiar JSON'} onClick={copiar}>
              {copiado ? <Check size={16} /> : <Copy size={16} />}
            </IconButton>
          </span>
        )}
      </div>

      <div className="px-5 py-3">
        {datos.tipo === 'vacio' && <p className="py-2 text-sm text-gray-500">{textoVacio}</p>}

        {datos.tipo !== 'vacio' && (verJson || datos.tipo === 'crudo') && (
          <pre className="max-h-96 overflow-auto rounded-lg bg-gray-50 p-3 text-xs text-gray-700">{jsonFormateado}</pre>
        )}

        {datos.tipo === 'entradas' && !verJson && (
          <>
            <dl className="divide-y divide-gray-100 text-sm">
              {mostradas.map((entrada) => (
                <div key={entrada.clave} className="flex items-start justify-between gap-4 py-2">
                  <dt className="shrink-0 text-gray-500">{entrada.etiqueta}</dt>
                  <dd className="min-w-0 text-right">
                    <Valor entrada={entrada} />
                  </dd>
                </div>
              ))}
            </dl>
            {entradas.length > VISIBLES && (
              <button type="button" className="mt-1 py-1 text-xs font-medium text-indigo-600 hover:text-indigo-700" onClick={() => setVerTodos((v) => !v)}>
                {verTodos ? 'Mostrar menos' : `Mostrar los ${entradas.length - VISIBLES} restantes`}
              </button>
            )}
          </>
        )}
      </div>
    </Card>
  )
}
