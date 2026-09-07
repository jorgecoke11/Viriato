import { CrudPage } from '../../../components/crud/CrudPage'
import type { CrudColumn, CrudFormConfig } from '../../../components/crud/types'
import * as rpaApi from '../api'
import type { ServicioDto } from '../api'

interface ServicioFormValues {
  [key: string]: string | boolean
  nombre: string
  descripcion: string
  activo: boolean
}

const columns: CrudColumn<ServicioDto>[] = [
  { key: 'nombre', label: 'Nombre', render: (s) => s.nombre },
  { key: 'descripcion', label: 'Descripción', render: (s) => s.descripcion ?? '—' },
  { key: 'activo', label: 'Activo', render: (s) => (s.activo ? 'Sí' : 'No') },
]

const createFields: CrudFormConfig<ServicioDto, ServicioFormValues, rpaApi.CreateServicioInput, rpaApi.UpdateServicioInput>['createFields'] = [
  { name: 'nombre', label: 'Nombre', required: true },
  { name: 'descripcion', label: 'Descripción', type: 'textarea' },
]

const editFields: CrudFormConfig<ServicioDto, ServicioFormValues, rpaApi.CreateServicioInput, rpaApi.UpdateServicioInput>['editFields'] = [
  { name: 'nombre', label: 'Nombre', required: true },
  { name: 'descripcion', label: 'Descripción', type: 'textarea' },
  { name: 'activo', label: 'Activo', type: 'checkbox' },
]

const form: CrudFormConfig<ServicioDto, ServicioFormValues, rpaApi.CreateServicioInput, rpaApi.UpdateServicioInput> = {
  createFields,
  editFields,
  emptyValues: { nombre: '', descripcion: '', activo: true },
  toEditValues: (servicio) => ({ nombre: servicio.nombre, descripcion: servicio.descripcion ?? '', activo: servicio.activo }),
  toCreateInput: (values) => ({ nombre: values.nombre, descripcion: values.descripcion || null }),
  toUpdateInput: (values) => ({ nombre: values.nombre, descripcion: values.descripcion || null, activo: values.activo }),
}

export function ServiciosPage() {
  return (
    <CrudPage<ServicioDto, ServicioFormValues, rpaApi.CreateServicioInput, rpaApi.UpdateServicioInput>
      title="Servicios"
      resourceKey="servicios-crud"
      getId={(servicio) => servicio.id}
      columns={columns}
      form={form}
      filters={{ mode: 'general', placeholder: 'Nombre del servicio…' }}
      api={{
        list: rpaApi.listServicios,
        create: rpaApi.createServicio,
        update: rpaApi.updateServicio,
        remove: rpaApi.deleteServicio,
      }}
      deleteConfirm={{
        message: '¿Seguro que quieres eliminar este servicio? Esta acción no se puede deshacer.',
      }}
    />
  )
}
