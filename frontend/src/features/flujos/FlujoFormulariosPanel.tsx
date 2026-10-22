import { useQueryClient } from '@tanstack/react-query'
import { CrudPage } from '../../components/crud/CrudPage'
import type { CrudColumn, CrudField, CrudFormConfig } from '../../components/crud/types'
import { errorDeEsquema } from '../../components/schema-form/comprobar'
import { EditorDeEsquema } from '../../components/schema-form/EditorDeEsquema'
import { contarCampos, leerEsquema } from '../../components/schema-form/esquema'
import * as flujosApi from './api'
import type { FormularioDto } from './api'

interface FormularioFormValues {
  [key: string]: string | boolean
  nombre: string
  descripcion: string
  esquemaJson: string
}

type Config = CrudFormConfig<FormularioDto, FormularioFormValues, flujosApi.GuardarFormularioRequest, flujosApi.GuardarFormularioRequest>

const campos: CrudField<FormularioFormValues>[] = [
  { name: 'nombre', label: 'Nombre', required: true, helpText: 'Para elegirlo en los creadores, p. ej. «Datos de cliente».' },
  { name: 'descripcion', label: 'Descripción' },
  {
    name: 'esquemaJson',
    label: 'Formulario',
    type: 'custom',
    validate: (valor) => errorDeEsquema(valor),
    render: ({ value, onChange }) => <EditorDeEsquema value={value} onChange={onChange} />,
  },
]

function aSolicitud(values: FormularioFormValues): flujosApi.GuardarFormularioRequest {
  return { nombre: values.nombre.trim(), descripcion: values.descripcion.trim() || null, esquemaJson: values.esquemaJson }
}

function resumen(f: FormularioDto) {
  const esquema = leerEsquema(f.esquemaJson)
  return esquema.ok ? `${contarCampos(esquema.esquema)} campos` : 'Esquema no válido'
}

/**
 * The forms of a process: the library its creators pick from. A form describes the data of a case — its fields, which are required, the
 * limits of each — and several creators can share one, so changing it is done once. Whoever creates a case sees the form of the creator
 * they picked; a launcher or a robot naming that creator has its data checked against it.
 */
export function FlujoFormulariosPanel({ flujoId }: { flujoId: string }) {
  const queryClient = useQueryClient()

  const columns: CrudColumn<FormularioDto>[] = [
    {
      key: 'nombre',
      label: 'Formulario',
      render: (f) => (
        <span className="flex flex-col">
          <span className="font-medium text-gray-900">{f.nombre}</span>
          {f.descripcion && <span className="text-xs text-gray-500">{f.descripcion}</span>}
        </span>
      ),
    },
    { key: 'campos', label: 'Campos', render: (f) => <span className="text-gray-600">{resumen(f)}</span> },
    {
      key: 'creadores',
      label: 'Lo usan',
      render: (f) => (f.creadores.length > 0 ? f.creadores.join(', ') : <span className="text-gray-400">Ningún creador</span>),
    },
  ]

  const form: Config = {
    createFields: campos,
    editFields: campos,
    emptyValues: { nombre: '', descripcion: '', esquemaJson: '' },
    toEditValues: (f) => ({ nombre: f.nombre, descripcion: f.descripcion ?? '', esquemaJson: f.esquemaJson }),
    toCreateInput: aSolicitud,
    toUpdateInput: aSolicitud,
    modalSize: 'xl',
  }

  // Creators, launchers and the new-case screens read the forms under their own keys.
  const refrescar = <R,>(r: R) => {
    void queryClient.invalidateQueries({ queryKey: ['flujo-formularios', flujoId] })
    void queryClient.invalidateQueries({ queryKey: ['creadores-disponibles'] })
    return r
  }

  return (
    <CrudPage<FormularioDto, FormularioFormValues, flujosApi.GuardarFormularioRequest, flujosApi.GuardarFormularioRequest>
      title="Formularios"
      resourceKey={`flujo-${flujoId}-formularios`}
      getId={(f) => f.id}
      columns={columns}
      form={form}
      filters={{ mode: 'general', placeholder: 'Nombre del formulario…' }}
      entidad={{ singular: 'formulario', plural: 'formularios' }}
      nombreDeFila={(f) => f.nombre}
      api={{
        list: async (filters) => {
          const term = filters.search?.toLowerCase() ?? ''
          const todos = await flujosApi.listFormularios(flujoId)
          const filtrados = todos.filter((f) => !term || f.nombre.toLowerCase().includes(term))
          return { items: filtrados, page: 1, pageSize: filtrados.length || 1, total: filtrados.length }
        },
        create: (input) => flujosApi.createFormulario(flujoId, input).then(refrescar),
        update: (id, input) => flujosApi.replaceFormulario(flujoId, id, input).then(refrescar),
        remove: (id) => flujosApi.deleteFormulario(flujoId, id).then(refrescar),
      }}
      deleteConfirm={{ message: '¿Seguro que quieres eliminar este formulario? Los casos que lo usaron conservan sus datos, pero ya no se comprobarán al editarlos.' }}
    />
  )
}
