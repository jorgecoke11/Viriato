import { CrudPage } from '../../../components/crud/CrudPage'
import type { CrudColumn, CrudFormConfig } from '../../../components/crud/types'
import * as casosApi from '../api'
import type { TipoDocumentoDto } from '../api'

interface TipoDocumentoFormValues {
  [key: string]: string | boolean
  nombre: string
  descripcion: string
  activo: boolean
}

const columns: CrudColumn<TipoDocumentoDto>[] = [
  { key: 'nombre', label: 'Nombre', render: (t) => t.nombre },
  { key: 'descripcion', label: 'Descripción', render: (t) => t.descripcion ?? '—' },
  { key: 'activo', label: 'Activo', render: (t) => (t.activo ? 'Sí' : 'No') },
]

const createFields: CrudFormConfig<
  TipoDocumentoDto,
  TipoDocumentoFormValues,
  casosApi.CreateTipoDocumentoInput,
  casosApi.UpdateTipoDocumentoInput
>['createFields'] = [
  { name: 'nombre', label: 'Nombre', required: true },
  { name: 'descripcion', label: 'Descripción', type: 'textarea' },
]

const editFields: CrudFormConfig<
  TipoDocumentoDto,
  TipoDocumentoFormValues,
  casosApi.CreateTipoDocumentoInput,
  casosApi.UpdateTipoDocumentoInput
>['editFields'] = [
  { name: 'nombre', label: 'Nombre', required: true },
  { name: 'descripcion', label: 'Descripción', type: 'textarea' },
  { name: 'activo', label: 'Activo', type: 'checkbox' },
]

const form: CrudFormConfig<TipoDocumentoDto, TipoDocumentoFormValues, casosApi.CreateTipoDocumentoInput, casosApi.UpdateTipoDocumentoInput> = {
  createFields,
  editFields,
  emptyValues: { nombre: '', descripcion: '', activo: true },
  toEditValues: (tipo) => ({ nombre: tipo.nombre, descripcion: tipo.descripcion ?? '', activo: tipo.activo }),
  toCreateInput: (values) => ({ nombre: values.nombre, descripcion: values.descripcion || null }),
  toUpdateInput: (values) => ({ nombre: values.nombre, descripcion: values.descripcion || null, activo: values.activo }),
}

export function TiposDocumentoPage() {
  return (
    <CrudPage<TipoDocumentoDto, TipoDocumentoFormValues, casosApi.CreateTipoDocumentoInput, casosApi.UpdateTipoDocumentoInput>
      title="Tipos de documento"
      resourceKey="tipos-documento-crud"
      getId={(tipo) => tipo.id}
      columns={columns}
      form={form}
      filters={{ mode: 'general', placeholder: 'Nombre del tipo…' }}
      api={{
        list: casosApi.listTiposDocumento,
        create: casosApi.createTipoDocumento,
        update: casosApi.updateTipoDocumento,
        remove: casosApi.deleteTipoDocumento,
      }}
      deleteConfirm={{
        message: '¿Seguro que quieres eliminar este tipo de documento? Esta acción no se puede deshacer.',
      }}
    />
  )
}
