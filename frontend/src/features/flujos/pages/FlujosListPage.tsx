import { useNavigate } from 'react-router-dom'
import { CrudPage } from '../../../components/crud/CrudPage'
import type { CrudColumn, CrudFormConfig } from '../../../components/crud/types'
import * as flujosApi from '../api'
import type { FlujoDto } from '../api'

interface FlujoFormValues {
  [key: string]: string | boolean
  nombre: string
  descripcion: string
  activo: boolean
}

const columns: CrudColumn<FlujoDto>[] = [
  { key: 'nombre', label: 'Nombre', render: (f) => f.nombre },
  { key: 'version', label: 'Versión activa', render: (f) => (f.numeroVersionActiva ? `v${f.numeroVersionActiva}` : 'Sin publicar') },
  { key: 'activo', label: 'Activo', render: (f) => (f.activo ? 'Sí' : 'No') },
]

const createFields: CrudFormConfig<FlujoDto, FlujoFormValues, flujosApi.CreateFlujoRequest, flujosApi.UpdateFlujoRequest>['createFields'] = [
  { name: 'nombre', label: 'Nombre', required: true },
  { name: 'descripcion', label: 'Descripción', type: 'textarea' },
]

const editFields: CrudFormConfig<FlujoDto, FlujoFormValues, flujosApi.CreateFlujoRequest, flujosApi.UpdateFlujoRequest>['editFields'] = [
  { name: 'nombre', label: 'Nombre', required: true },
  { name: 'descripcion', label: 'Descripción', type: 'textarea' },
  { name: 'activo', label: 'Activo (visible al crear casos)', type: 'checkbox' },
]

const form: CrudFormConfig<FlujoDto, FlujoFormValues, flujosApi.CreateFlujoRequest, flujosApi.UpdateFlujoRequest> = {
  createFields,
  editFields,
  emptyValues: { nombre: '', descripcion: '', activo: true },
  toEditValues: (flujo) => ({ nombre: flujo.nombre, descripcion: flujo.descripcion ?? '', activo: flujo.activo }),
  toCreateInput: (values) => ({ nombre: values.nombre, descripcion: values.descripcion || null }),
  toUpdateInput: (values) => ({ nombre: values.nombre, descripcion: values.descripcion || null, activo: values.activo }),
}

export function FlujosListPage() {
  const navigate = useNavigate()

  return (
    <CrudPage<FlujoDto, FlujoFormValues, flujosApi.CreateFlujoRequest, flujosApi.UpdateFlujoRequest>
      title="Procesos"
      resourceKey="flujos-admin-crud"
      getId={(flujo) => flujo.id}
      columns={columns}
      form={form}
      filters={{ mode: 'general', placeholder: 'Nombre del proceso…' }}
      api={{
        list: flujosApi.listFlujos,
        create: flujosApi.createFlujo,
        update: flujosApi.updateFlujo,
        remove: flujosApi.deleteFlujo,
      }}
      renderRowExtra={(flujo) => (
        <button type="button" className="text-gray-500 hover:text-gray-900" onClick={() => navigate(`/admin/flujos/${flujo.id}`)}>
          Configurar
        </button>
      )}
    />
  )
}
