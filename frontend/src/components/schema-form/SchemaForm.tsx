import { useId, useState } from 'react'
import { estadoVacio } from './esquema'
import type { CampoEsquema, Esquema, EstadoCampo, EstadoFormulario } from './esquema'

const ENTRADA = 'field'
const BORDE_OK = ''
const BORDE_ERROR = 'border-red-500'

const unir = (ruta: string, nombre: string) => (ruta === '' ? nombre : `${ruta}.${nombre}`)

interface SchemaFormProps {
  esquema: Esquema
  estado: EstadoFormulario
  onChange: (estado: EstadoFormulario) => void
  /** Message to show under each field, keyed by the field's dotted path (`lineas.1.sku`). */
  errores?: Record<string, string>
  disabled?: boolean
}

/**
 * Draws a JSON schema (see esquema.ts) as a form and reports the filled-in state. It knows nothing about what the
 * JSON is for: a Caso's business data, a process parameter, a step's settings. Converting between this state and
 * the JSON — `aEstado` / `aDatos` — and checking it — `validarDatos` — is the caller's, so it can decide when to
 * start showing errors.
 */
export function SchemaForm({ esquema, estado, onChange, errores = {}, disabled = false }: SchemaFormProps) {
  return (
    <div className="flex flex-col gap-4">
      <Campos esquema={esquema} estado={estado} onChange={onChange} ruta="" errores={errores} disabled={disabled} />
    </div>
  )
}

interface CamposProps {
  esquema: CampoEsquema
  estado: EstadoFormulario
  onChange: (estado: EstadoFormulario) => void
  ruta: string
  errores: Record<string, string>
  disabled: boolean
}

function Campos({ esquema, estado, onChange, ruta, errores, disabled }: CamposProps) {
  const requeridos = new Set(esquema.required ?? [])

  return (
    <>
      {Object.entries(esquema.properties ?? {}).map(([nombre, campo]) => (
        <Campo
          key={nombre}
          nombre={nombre}
          campo={campo}
          valor={estado[nombre]}
          requerido={requeridos.has(nombre)}
          ruta={unir(ruta, nombre)}
          errores={errores}
          disabled={disabled}
          onChange={(valor) => onChange({ ...estado, [nombre]: valor })}
        />
      ))}
    </>
  )
}

interface CampoProps {
  nombre: string
  campo: CampoEsquema
  valor: EstadoCampo | undefined
  requerido: boolean
  ruta: string
  errores: Record<string, string>
  disabled: boolean
  onChange: (valor: EstadoCampo) => void
}

function Etiqueta({ id, texto, requerido }: { id?: string; texto: string; requerido: boolean }) {
  return (
    <label htmlFor={id} className="text-sm font-medium text-gray-700">
      {texto}
      {requerido && <span className="text-red-500"> *</span>}
    </label>
  )
}

function Ayuda({ campo, error }: { campo: CampoEsquema; error?: string }) {
  return (
    <>
      {campo.description && <p className="text-xs text-gray-500">{campo.description}</p>}
      {error && <p className="text-xs text-red-600">{error}</p>}
    </>
  )
}

function Campo({ nombre, campo, valor, requerido, ruta, errores, disabled, onChange }: CampoProps) {
  const id = useId()
  const titulo = campo.title?.trim() || nombre
  const error = errores[ruta]
  const borde = error ? BORDE_ERROR : BORDE_OK

  if (campo.type === 'boolean') {
    return (
      <div className="flex flex-col gap-1">
        <label className="flex items-center gap-2 text-sm text-gray-700">
          <input
            type="checkbox"
            className="accent-indigo-600"
            checked={valor === true}
            disabled={disabled}
            onChange={(e) => onChange(e.target.checked)}
          />
          {titulo}
        </label>
        <Ayuda campo={campo} error={error} />
      </div>
    )
  }

  if (campo.type === 'object') {
    return (
      <fieldset className="flex flex-col gap-3 rounded-lg border border-gray-200 p-3">
        <legend className="px-1 text-sm font-medium text-gray-700">
          {titulo}
          {requerido && <span className="text-red-500"> *</span>}
        </legend>
        {campo.description && <p className="text-xs text-gray-500">{campo.description}</p>}
        <Campos
          esquema={campo}
          estado={(valor ?? {}) as EstadoFormulario}
          onChange={onChange}
          ruta={ruta}
          errores={errores}
          disabled={disabled}
        />
        {error && <p className="text-xs text-red-600">{error}</p>}
      </fieldset>
    )
  }

  if (campo.type === 'array') {
    return (
      <div className="flex flex-col gap-1">
        <Etiqueta texto={titulo} requerido={requerido} />
        <Lista
          campo={campo}
          elementos={(Array.isArray(valor) ? valor : []) as EstadoCampo[]}
          ruta={ruta}
          errores={errores}
          disabled={disabled}
          onChange={onChange}
        />
        <Ayuda campo={campo} error={error} />
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-1">
      <Etiqueta id={id} texto={titulo} requerido={requerido} />
      <Escalar id={id} campo={campo} valor={typeof valor === 'string' ? valor : ''} requerido={requerido} borde={borde} disabled={disabled} onChange={onChange} />
      <Ayuda campo={campo} error={error} />
    </div>
  )
}

interface EscalarProps {
  id?: string
  campo: CampoEsquema
  valor: string
  requerido: boolean
  borde: string
  disabled: boolean
  onChange: (valor: string) => void
}

// One text/number/date/choice input: what a field is, or what each row of a list of simple values is.
function Escalar({ id, campo, valor, requerido, borde, disabled, onChange }: EscalarProps) {
  if (campo.enum) {
    return (
      <select id={id} className={`${ENTRADA} ${borde}`} value={valor} disabled={disabled} onChange={(e) => onChange(e.target.value)}>
        {!requerido && <option value="">— sin elegir —</option>}
        {requerido && valor === '' && <option value="">Selecciona…</option>}
        {campo.enum.map((opcion, i) => (
          <option key={String(opcion)} value={String(opcion)}>
            {campo.enumNames?.[i] ?? String(opcion)}
          </option>
        ))}
      </select>
    )
  }

  if (campo.type === 'number' || campo.type === 'integer') {
    return (
      <div className="flex items-center gap-2">
        <input
          id={id}
          type="number"
          inputMode="decimal"
          step={campo.type === 'integer' ? 1 : 'any'}
          min={campo.minimum}
          max={campo.maximum}
          className={`${ENTRADA} ${borde}`}
          value={valor}
          disabled={disabled}
          onChange={(e) => onChange(e.target.value)}
        />
        {campo['x-suffix'] && <span className="shrink-0 text-sm text-gray-500">{campo['x-suffix']}</span>}
      </div>
    )
  }

  if (campo['x-widget'] === 'textarea') {
    return (
      <textarea id={id} rows={4} className={`${ENTRADA} ${borde}`} value={valor} disabled={disabled} onChange={(e) => onChange(e.target.value)} />
    )
  }

  const tipoEntrada = campo.format === 'date' ? 'date' : campo.format === 'date-time' ? 'datetime-local' : campo.format === 'email' ? 'email' : 'text'
  return (
    <input
      id={id}
      type={tipoEntrada}
      maxLength={campo.maxLength}
      className={`${ENTRADA} ${borde}`}
      value={valor}
      disabled={disabled}
      onChange={(e) => onChange(e.target.value)}
    />
  )
}

interface ListaProps {
  campo: CampoEsquema
  elementos: EstadoCampo[]
  ruta: string
  errores: Record<string, string>
  disabled: boolean
  onChange: (elementos: EstadoCampo[]) => void
}

function Lista({ campo, elementos, ruta, errores, disabled, onChange }: ListaProps) {
  const items = campo.items!
  const [comoTexto, setComoTexto] = useState(false)

  // A list of choices: tick the ones that apply.
  if (items.enum && items.type !== 'object') {
    const marcados = new Set(elementos.map(String))
    return (
      <div className="flex flex-wrap gap-x-4 gap-y-1">
        {items.enum.map((opcion, i) => {
          const valor = String(opcion)
          return (
            <label key={valor} className="flex items-center gap-2 text-sm text-gray-700">
              <input
                type="checkbox"
                className="accent-indigo-600"
                checked={marcados.has(valor)}
                disabled={disabled}
                onChange={(e) =>
                  onChange(
                    // Keep the order of the options, not the order they were ticked in.
                    items.enum!.map(String).filter((o) => (o === valor ? e.target.checked : marcados.has(o))),
                  )
                }
              />
              {items.enumNames?.[i] ?? valor}
            </label>
          )
        })}
      </div>
    )
  }

  const cambiar = (indice: number, valor: EstadoCampo) => onChange(elementos.map((e, i) => (i === indice ? valor : e)))
  const quitar = (indice: number) => onChange(elementos.filter((_, i) => i !== indice))
  const anadir = () => onChange([...elementos, estadoVacio(items)])

  if (items.type === 'object') {
    return (
      <div className="flex flex-col gap-3">
        {elementos.map((elemento, i) => (
          <div key={i} className="flex flex-col gap-3 rounded-lg border border-gray-200 bg-gray-50/50 p-3">
            <div className="flex items-center justify-between">
              <span className="text-xs font-medium text-gray-500">#{i + 1}</span>
              <button type="button" className="text-xs text-gray-500 hover:text-red-600" disabled={disabled} onClick={() => quitar(i)}>
                Quitar
              </button>
            </div>
            <Campos
              esquema={items}
              estado={elemento as EstadoFormulario}
              onChange={(e) => cambiar(i, e)}
              ruta={unir(ruta, String(i))}
              errores={errores}
              disabled={disabled}
            />
          </div>
        ))}
        <button type="button" className="self-start text-sm text-indigo-600 hover:text-indigo-800" disabled={disabled} onClick={anadir}>
          + Añadir elemento
        </button>
      </div>
    )
  }

  // A list of plain values: one row each, or all at once as text (one per line) when there are many.
  if (comoTexto && items.type === 'string') {
    return (
      <div className="flex flex-col gap-1">
        <textarea
          rows={Math.min(12, Math.max(4, elementos.length + 1))}
          className={`${ENTRADA} ${BORDE_OK} font-mono text-xs`}
          value={elementos.join('\n')}
          disabled={disabled}
          onChange={(e) => onChange(e.target.value.split('\n'))}
        />
        <button type="button" className="self-start text-xs text-indigo-600 hover:text-indigo-800" onClick={() => setComoTexto(false)}>
          Ver como lista
        </button>
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-2">
      {elementos.map((elemento, i) => (
        <div key={i} className="flex items-start gap-2">
          <div className="flex-1">
            <Escalar
              campo={items}
              valor={typeof elemento === 'string' ? elemento : ''}
              requerido
              borde={errores[unir(ruta, String(i))] ? BORDE_ERROR : BORDE_OK}
              disabled={disabled}
              onChange={(v) => cambiar(i, v)}
            />
            {errores[unir(ruta, String(i))] && <p className="text-xs text-red-600">{errores[unir(ruta, String(i))]}</p>}
          </div>
          <button type="button" className="mt-2 text-xs text-gray-500 hover:text-red-600" disabled={disabled} onClick={() => quitar(i)}>
            Quitar
          </button>
        </div>
      ))}
      <div className="flex items-center gap-4">
        <button type="button" className="text-sm text-indigo-600 hover:text-indigo-800" disabled={disabled} onClick={anadir}>
          + Añadir
        </button>
        {items.type === 'string' && (
          <button type="button" className="text-xs text-gray-500 hover:text-gray-800" onClick={() => setComoTexto(true)}>
            Editar como texto (uno por línea)
          </button>
        )}
      </div>
    </div>
  )
}
