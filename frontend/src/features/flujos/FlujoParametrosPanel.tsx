import { useState } from 'react'
import { CrudPage } from '../../components/crud/CrudPage'
import type { CrudColumn, CrudFormConfig } from '../../components/crud/types'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Modal } from '../../components/ui/Modal'
import { useToast } from '../../lib/toast/useToast'
import * as flujosApi from './api'
import type { FlujoParametroDto } from './api'
import { errorDeJson, formatearJson, leerJson, pareceJson, resumenJson } from './jsonValor'

interface ParametroFormValues {
  [key: string]: string | boolean
  codigo: string
  valor: string
  descripcion: string
  etiqueta: string
  editablePorUsuario: boolean
}

type Config = CrudFormConfig<FlujoParametroDto, ParametroFormValues, flujosApi.CreateFlujoParametroRequest, flujosApi.UpdateFlujoParametroRequest>

const VALOR_AYUDA =
  'Texto plano, visible para quien pueda ver este proceso. Si es un JSON se validará mientras escribes y se mostrará formateado en la lista. Para contraseñas usa Credenciales, no un parámetro.'

const campoValor: Config['createFields'][number] = {
  name: 'valor',
  label: 'Valor',
  type: 'textarea',
  rows: 6,
  mono: true,
  helpText: VALOR_AYUDA,
  validate: errorDeJson,
}

// Opening a parameter to the people working the process: they change its value from the dashboard, under this name.
const camposDelPanel: Config['createFields'] = [
  {
    name: 'editablePorUsuario',
    label: 'Los usuarios pueden cambiarlo desde el panel',
    type: 'checkbox',
  },
  {
    name: 'etiqueta',
    label: 'Nombre en el panel',
    helpText: 'Lo que verá el usuario en lugar del código, p. ej. «IVA (%)».',
    visibleWhen: (values) => Boolean(values.editablePorUsuario),
  },
]

const createFields: Config['createFields'] = [
  {
    name: 'codigo',
    label: 'Código',
    required: true,
    helpText: 'Con este nombre lo pedirá el robot, p. ej. iva. Solo minúsculas, números, punto, guion y guion bajo. No podrá cambiarse después.',
  },
  campoValor,
  { name: 'descripcion', label: 'Descripción' },
  ...camposDelPanel,
]

const editFields: Config['editFields'] = [
  { name: 'codigo', label: 'Código', disabled: true },
  campoValor,
  { name: 'descripcion', label: 'Descripción' },
  ...camposDelPanel,
]

// A JSON value is not shown inline: a summary, and a modal with the document properly indented. A value that
// looks like JSON but does not parse is flagged, since a robot reading it would fail.
function ValorParametro({ parametro }: { parametro: FlujoParametroDto }) {
  const { showToast } = useToast()
  const [abierto, setAbierto] = useState(false)
  const { valor } = parametro

  if (valor === '') return <span className="text-gray-400">(vacío)</span>

  if (!pareceJson(valor)) {
    return (
      <span className="block max-w-md truncate" title={valor}>
        {valor}
      </span>
    )
  }

  const json = leerJson(valor)
  if (!json.ok) {
    return (
      <span className="flex max-w-md items-center gap-2">
        <span className="shrink-0 rounded bg-red-50 px-1.5 py-0.5 text-xs font-medium text-red-700">JSON no válido</span>
        <span className="truncate font-mono text-xs text-gray-500" title={valor}>
          {valor}
        </span>
      </span>
    )
  }

  const formateado = formatearJson(json.valor)

  async function copiar() {
    try {
      await navigator.clipboard.writeText(formateado)
      showToast('success', 'JSON copiado.')
    } catch {
      showToast('error', 'No se pudo copiar. Selecciónalo manualmente.')
    }
  }

  return (
    <>
      <span className="flex items-center gap-2">
        <span className="shrink-0 rounded bg-indigo-50 px-1.5 py-0.5 text-xs font-medium text-indigo-700">JSON</span>
        <span className="text-gray-600">{resumenJson(json.valor)}</span>
        <button type="button" className="text-indigo-600 hover:text-indigo-800" onClick={() => setAbierto(true)}>
          Ver
        </button>
      </span>
      <Modal
        open={abierto}
        size="lg"
        title={
          <span>
            <span className="font-mono">{parametro.codigo}</span> · {resumenJson(json.valor)}
          </span>
        }
        onClose={() => setAbierto(false)}
        footer={
          <div className="flex justify-end gap-2">
            <Button type="button" variant="ghost" onClick={copiar}>
              Copiar
            </Button>
            <Button type="button" onClick={() => setAbierto(false)}>
              Cerrar
            </Button>
          </div>
        }
      >
        <pre className="overflow-auto whitespace-pre-wrap break-words rounded-lg bg-gray-50 p-3 font-mono text-xs text-gray-800">{formateado}</pre>
      </Modal>
    </>
  )
}

const columns: CrudColumn<FlujoParametroDto>[] = [
  { key: 'codigo', label: 'Código', render: (p) => <span className="font-mono text-xs">{p.codigo}</span> },
  { key: 'valor', label: 'Valor', render: (p) => <ValorParametro parametro={p} /> },
  { key: 'descripcion', label: 'Descripción', render: (p) => p.descripcion ?? '—' },
  {
    key: 'panel',
    label: 'En el panel',
    render: (p) =>
      p.editablePorUsuario ? (
        <Badge tone="info" title="Los usuarios lo cambian desde el panel">
          {p.etiqueta ?? 'Editable'}
        </Badge>
      ) : (
        <span className="text-gray-400">Solo administradores</span>
      ),
  },
]

const form: Config = {
  createFields,
  editFields,
  emptyValues: { codigo: '', valor: '', descripcion: '', etiqueta: '', editablePorUsuario: false },
  toEditValues: (p) => ({
    codigo: p.codigo,
    valor: p.valor,
    descripcion: p.descripcion ?? '',
    etiqueta: p.etiqueta ?? '',
    editablePorUsuario: p.editablePorUsuario,
  }),
  toCreateInput: (values) => ({
    codigo: values.codigo.trim(),
    valor: values.valor,
    descripcion: values.descripcion.trim() || null,
    editablePorUsuario: values.editablePorUsuario,
    etiqueta: values.editablePorUsuario ? values.etiqueta.trim() || null : null,
  }),
  toUpdateInput: (values) => ({
    valor: values.valor,
    descripcion: values.descripcion.trim() || null,
    editablePorUsuario: values.editablePorUsuario,
    // An empty label clears it; closing the parameter to users drops the label with it.
    etiqueta: values.editablePorUsuario ? values.etiqueta.trim() : '',
  }),
}

// Settings of one process that its robots read at run time (what the old platform kept in a global
// "parámetros" table, now scoped to the process): e.g. iva, n_maximo_carrito, or a JSON list of products.
export function FlujoParametrosPanel({ flujoId }: { flujoId: string }) {
  return (
    <CrudPage<FlujoParametroDto, ParametroFormValues, flujosApi.CreateFlujoParametroRequest, flujosApi.UpdateFlujoParametroRequest>
      title="Parámetros del proceso"
      resourceKey={`flujo-${flujoId}-parametros`}
      getId={(p) => p.id}
      columns={columns}
      form={form}
      filters={{ mode: 'general', placeholder: 'Código del parámetro…' }}
      api={{
        list: async (filters) => {
          const term = filters.search?.toLowerCase() ?? ''
          const all = await flujosApi.listFlujoParametros(flujoId)
          const filtered = all.filter((p) => !term || p.codigo.includes(term) || p.valor.toLowerCase().includes(term))
          return { items: filtered, page: 1, pageSize: filtered.length || 1, total: filtered.length }
        },
        create: (input) => flujosApi.createFlujoParametro(flujoId, input),
        update: (id, input) => flujosApi.updateFlujoParametro(flujoId, id, input),
        remove: (id) => flujosApi.deleteFlujoParametro(flujoId, id),
      }}
      deleteConfirm={{
        message: '¿Seguro que quieres eliminar este parámetro? Los robots del proceso que lo usen fallarán hasta que lo vuelvas a crear.',
      }}
    />
  )
}
