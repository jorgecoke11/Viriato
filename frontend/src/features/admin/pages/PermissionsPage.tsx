import { CrudPage } from '../../../components/crud/CrudPage'
import type { CrudColumn } from '../../../components/crud/types'
import { useAuth } from '../../auth/useAuth'
import * as adminApi from '../api'
import type { PermissionDto } from '../api'

const columns: CrudColumn<PermissionDto>[] = [
  { key: 'name', label: 'Permiso', render: (p) => p.name },
  { key: 'module', label: 'Módulo', render: (p) => p.module },
  { key: 'action', label: 'Acción', render: (p) => p.action },
  { key: 'description', label: 'Descripción', render: (p) => p.description ?? '—' },
]

// Read-only: permissions are declared in code and synced to the database on startup (§5.1),
// so there is no create/edit/delete form here — the database is never their source of truth.
export function PermissionsPage() {
  const { can } = useAuth()

  if (!can('roles.manage')) {
    return <p className="text-gray-600">No tienes permiso para ver esta sección.</p>
  }

  return (
    <CrudPage
      title="Permisos"
      resourceKey="admin-permissions-crud"
      getId={(permission) => permission.id}
      columns={columns}
      api={{ list: adminApi.listPermissions }}
    />
  )
}
