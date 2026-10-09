import { Braces, Check, ChevronDown, Copy, List } from 'lucide-react'
import { useState } from 'react'
import { Card } from '../../components/ui/Card'
import { IconButton } from '../../components/ui/IconButton'
import { useToast } from '../../lib/toast/useToast'
import { leerDatosDelCaso, type EntradaDeDato } from './datosDelCaso'

const VISIBLES = 8

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
          <pre className="mt-2 max-h-56 overflow-auto rounded-lg bg-gray-50 p-2.5 text-left text-xs text-gray-700">{valor.json}</pre>
        </details>
      )
  }
}

/**
 * The business data of the Caso — what the robots of the process work from — as a readable list instead of JSON: one
 * row per field, with its label and its value. Lists and nested objects stay folded until asked for. The JSON itself is
 * one click away, and can be copied.
 */
export function DatosDelCasoCard({ datosJson }: { datosJson: string | null }) {
  const { showToast } = useToast()
  const datos = leerDatosDelCaso(datosJson)
  const [verJson, setVerJson] = useState(false)
  const [verTodos, setVerTodos] = useState(false)
  const [copiado, setCopiado] = useState(false)

  const jsonFormateado = (() => {
    if (datosJson === null) return ''
    try {
      return JSON.stringify(JSON.parse(datosJson), null, 2)
    } catch {
      return datosJson
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
        <h2 className="text-sm font-semibold text-gray-900">Datos del caso</h2>
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
        {datos.tipo === 'vacio' && <p className="py-2 text-sm text-gray-500">Este caso no tiene datos de negocio.</p>}

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
