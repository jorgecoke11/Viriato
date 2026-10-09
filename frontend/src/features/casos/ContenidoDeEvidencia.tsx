import { Check, ChevronDown, Copy } from 'lucide-react'
import { useState } from 'react'
import { leerDatosDelCaso, type ValorDeDato } from './datosDelCaso'

// An evidence's contenidoJson is free-form: a plain message, or an object/array worth reading as a few facts.
function analizar(raw: string): { tipo: 'texto'; texto: string } | { tipo: 'json'; texto: string } {
  try {
    const valor: unknown = JSON.parse(raw)
    if (typeof valor === 'string') return { tipo: 'texto', texto: valor }
    return { tipo: 'json', texto: JSON.stringify(valor, null, 2) }
  } catch {
    return { tipo: 'texto', texto: raw }
  }
}

function textoDeValor(valor: ValorDeDato): string {
  return valor.tipo === 'vacio' ? '—' : valor.tipo === 'compuesto' ? valor.resumen : valor.texto
}

const HECHOS_VISIBLES = 4
const LARGO_DE_NOTA = 220

/**
 * What an evidence says, at a glance: a message as text (folded when long), an object as its first few fields in small tiles,
 * with the whole JSON one click away. The same for any evidence that carries content.
 */
export function ContenidoDeEvidencia({ raw }: { raw: string }) {
  const [abierto, setAbierto] = useState(false)
  const [copiado, setCopiado] = useState(false)
  const contenido = analizar(raw)

  if (contenido.tipo === 'texto') {
    const largo = contenido.texto.length > LARGO_DE_NOTA
    return (
      <div className="text-sm text-gray-700">
        <p className={`break-words whitespace-pre-wrap ${largo && !abierto ? 'line-clamp-3' : ''}`}>{contenido.texto}</p>
        {largo && (
          <button type="button" className="mt-1 text-xs font-medium text-indigo-600 hover:text-indigo-700" onClick={() => setAbierto((a) => !a)}>
            {abierto ? 'Ver menos' : 'Ver más'}
          </button>
        )}
      </div>
    )
  }

  const datos = leerDatosDelCaso(raw)
  const hechos = datos.tipo === 'entradas' ? datos.entradas.slice(0, HECHOS_VISIBLES) : []

  async function copiar() {
    try {
      await navigator.clipboard.writeText(contenido.texto)
      setCopiado(true)
      window.setTimeout(() => setCopiado(false), 1500)
    } catch {
      // The clipboard can be blocked (insecure context, permissions): nothing useful to say about it.
    }
  }

  return (
    <div className="flex flex-col gap-2">
      {hechos.length > 0 && (
        <dl className="grid grid-cols-2 gap-2 sm:grid-cols-4">
          {hechos.map((hecho) => (
            <div key={hecho.clave} className="min-w-0 rounded-lg bg-gray-50 px-3 py-2">
              <dt className="truncate text-[11px] font-medium tracking-wide text-gray-500 uppercase" title={hecho.etiqueta}>
                {hecho.etiqueta}
              </dt>
              <dd className="num mt-0.5 truncate text-sm font-semibold text-gray-900" title={textoDeValor(hecho.valor)}>
                {textoDeValor(hecho.valor)}
              </dd>
            </div>
          ))}
        </dl>
      )}
      <div>
        <button type="button" aria-expanded={abierto} className="inline-flex items-center gap-1 text-xs font-medium text-indigo-600 hover:text-indigo-700" onClick={() => setAbierto((a) => !a)}>
          {abierto ? 'Ocultar el JSON' : 'Ver el JSON completo'}
          <ChevronDown size={13} aria-hidden="true" className={`transition-transform ${abierto ? 'rotate-180' : ''}`} />
        </button>
      </div>
      {abierto && (
        <div className="rounded-lg border border-gray-200">
          <div className="flex items-center justify-between border-b border-gray-200 bg-gray-50 px-3 py-1.5">
            <span className="text-xs font-medium text-gray-500">JSON</span>
            <button type="button" className="inline-flex items-center gap-1 text-xs font-medium text-gray-500 hover:text-gray-900" onClick={copiar}>
              {copiado ? <Check size={12} aria-hidden="true" /> : <Copy size={12} aria-hidden="true" />}
              {copiado ? 'Copiado' : 'Copiar'}
            </button>
          </div>
          <pre className="max-h-72 overflow-auto p-3 text-xs text-gray-800">{contenido.texto}</pre>
        </div>
      )}
    </div>
  )
}
