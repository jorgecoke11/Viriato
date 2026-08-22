import { useState } from 'react'
import { CrudPage } from '../../../components/crud/CrudPage'
import type { CrudColumn, CrudFormConfig } from '../../../components/crud/types'
import { useAuth } from '../../auth/useAuth'
import * as adminApi from '../api'
import type { RoleDto } from '../api'
import { RolePermissionsModal } from './RolePermissionsModal'

interface RoleFormValues {
  [key: string]: string
  name: string
  description: string
}

const columns: CrudColumn<RoleDto>[] = [
  { key: 'name', label: 'Nombre', render: (role) => role.name },
  { key: 'description', label: 'Descripción', render: (role) => role.description ?? '—' },
  { key: 'system', label: 'Sistema', render: (role) => (role.isSystem ? 'Sí' : 'No') },
  {
    key: 'permissions',
    label: 'Permisos',
    render: (role) => (
      <div className="flex flex-wrap gap-1">
        {role.permissions.length === 0
          ? <span className="text-gray-400">—</span>
          : role.permissions.map((p) => (
              <span key={p} className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-700">
                {p}
              </span>
            ))}
      </div>
    ),
  },
]

const form: CrudFormConfig<RoleDto, RoleFormValues, adminApi.CreateRoleInput, adminApi.UpdateRoleInput> = {
  fields: [
    { name: 'name', label: 'Nombre', required: true },
    { name: 'description', label: 'Descripción', type: 'textarea' },
  ],
  emptyValues: { name: '', description: '' },
  toEditValues: (role) => ({ name: role.name, description: role.description ?? '' }),
  toCreateInput: (values) => ({ name: values.name, description: values.description }),
  toUpdateInput: (values) => ({ name: values.name, description: values.description }),
}

export function RolesPage() {
  const { can } = useAuth()
  const [permissionsRole, setPermissionsRole] = useState<RoleDto | null>(null)

  if (!can('roles.manage')) {
    return <p className="text-gray-600">No tienes permiso para ver esta sección.</p>
  }

  return (
    <>
      <CrudPage<RoleDto, RoleFormValues, adminApi.CreateRoleInput, adminApi.UpdateRoleInput>
        title="Roles"
        resourceKey="admin-roles-crud"
        getId={(role) => role.id}
        columns={columns}
        form={form}
        api={{
          list: adminApi.listRoles,
          create: adminApi.createRole,
          update: adminApi.updateRole,
          remove: adminApi.deleteRole,
        }}
        renderRowExtra={(role) => (
          <button type="button" className="text-gray-500 hover:text-gray-900" onClick={() => setPermissionsRole(role)}>
            Permisos
          </button>
        )}
      />
      {permissionsRole && <RolePermissionsModal role={permissionsRole} onClose={() => setPermissionsRole(null)} />}
    </>
  )
}
