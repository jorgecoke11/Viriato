import { ListOrdered } from 'lucide-react'
import { useState } from 'react'
import { CrudPage } from '../../../components/crud/CrudPage'
import type { CrudColumn, CrudFormConfig } from '../../../components/crud/types'
import * as rpaApi from '../api'
import type { EquipoDto } from '../api'
import { ActivoBadge } from '../../../components/ui/ActivoBadge'
import { IconButton } from '../../../components/ui/IconButton'
import { DespachoEquipoModal } from '../despacho/DespachoEquipoModal'

interface EquipoFormValues {
  [key: string]: string | boolean
  nombre: string
  descripcion: string
  activo: boolean
}

const columns: CrudColumn<EquipoDto>[] = [
  { key: 'nombre', label: 'Nombre', render: (e) => e.nombre },
  { key: 'descripcion', label: 'Descripción', render: (e) => e.descripcion ?? '—' },
  { key: 'activo', label: 'Activo', render: (e) => <ActivoBadge activo={e.activo} /> },
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
  const [despachando, setDespachando] = useState<EquipoDto | null>(null)

  return (
    <>
    <CrudPage<EquipoDto, EquipoFormValues, rpaApi.CreateEquipoInput, rpaApi.UpdateEquipoInput>
      title="Equipos"
      resourceKey="equipos-crud"
      getId={(equipo) => equipo.id}
      columns={columns}
      form={form}
      filters={{ mode: 'general', placeholder: 'Nombre del equipo…' }}
      renderRowExtra={(equipo) => (
        <IconButton size="sm" label={`Despacho de ${equipo.nombre}: orden de los servicios`} onClick={() => setDespachando(equipo)}>
          <ListOrdered size={16} />
        </IconButton>
      )}
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
    <DespachoEquipoModal equipo={despachando ? { id: despachando.id, nombre: despachando.nombre } : null} onClose={() => setDespachando(null)} />
    </>
  )
}
