import { CrudPage } from '../../../components/crud/CrudPage'
import type { CrudColumn, CrudFormConfig } from '../../../components/crud/types'
import * as rpaApi from '../api'
import type { EquipoDto } from '../api'

interface EquipoFormValues {
  [key: string]: string | boolean
  nombre: string
  descripcion: string
  activo: boolean
}

const columns: CrudColumn<EquipoDto>[] = [
  { key: 'nombre', label: 'Nombre', render: (e) => e.nombre },
  { key: 'descripcion', label: 'Descripción', render: (e) => e.descripcion ?? '—' },
  { key: 'activo', label: 'Activo', render: (e) => (e.activo ? 'Sí' : 'No') },
]

const createFields: CrudFormConfig<EquipoDto, EquipoFormValues, rpaApi.CreateEquipoInput, rpaApi.UpdateEquipoInput>['createFields'] = [
  { name: 'nombre', label: 'Nombre', required: true },
  { name: 'descripcion', label: 'Descripción', type: 'textarea' },
]

const editFields: CrudFormConfig<EquipoDto, EquipoFormValues, rpaApi.CreateEquipoInput, rpaApi.UpdateEquipoInput>['editFields'] = [
  { name: 'nombre', label: 'Nombre', required: true },
  { name: 'descripcion', label: 'Descripción', type: 'textarea' },
  { name: 'activo', label: 'Activo', type: 'checkbox' },
]

const form: CrudFormConfig<EquipoDto, EquipoFormValues, rpaApi.CreateEquipoInput, rpaApi.UpdateEquipoInput> = {
  createFields,
  editFields,
  emptyValues: { nombre: '', descripcion: '', activo: true },
  toEditValues: (equipo) => ({ nombre: equipo.nombre, descripcion: equipo.descripcion ?? '', activo: equipo.activo }),
  toCreateInput: (values) => ({ nombre: values.nombre, descripcion: values.descripcion || null }),
  toUpdateInput: (values) => ({ nombre: values.nombre, descripcion: values.descripcion || null, activo: values.activo }),
}

export function EquiposPage() {
  return (
    <CrudPage<EquipoDto, EquipoFormValues, rpaApi.CreateEquipoInput, rpaApi.UpdateEquipoInput>
      title="Equipos"
      resourceKey="equipos-crud"
      getId={(equipo) => equipo.id}
      columns={columns}
      form={form}
      filters={{ mode: 'general', placeholder: 'Nombre del equipo…' }}
      api={{
        list: rpaApi.listEquipos,
        create: rpaApi.createEquipo,
        update: rpaApi.updateEquipo,
        remove: rpaApi.deleteEquipo,
      }}
      deleteConfirm={{
        message: '¿Seguro que quieres eliminar este equipo? Esta acción no se puede deshacer.',
      }}
    />
  )
}
