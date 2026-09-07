import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { Button } from '../../../components/ui/Button'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/apiClient'
import { useToast } from '../../../lib/toast/useToast'
import * as adminApi from '../api'
import type { RoleDto } from '../api'

export function RolePermissionsModal({ role, onClose }: { role: RoleDto | null; onClose: () => void }) {
  const open = role !== null
  const queryClient = useQueryClient()
  const { showToast } = useToast()

  // The modal now stays mounted at all times (needed so it can animate its own exit), so the role
  // it displays has to be frozen here — the caller nulls its `role` state the instant it closes.
  const [activeRole, setActiveRole] = useState(role)
  const [selected, setSelected] = useState<Set<string>>(new Set(role?.permissions ?? []))
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (role) {
      setActiveRole(role)
      setSelected(new Set(role.permissions))
      setError(null)
    }
  }, [role])

  const permissionsQuery = useQuery({
    queryKey: ['admin-permissions'],
    queryFn: () => adminApi.listPermissions(),
    enabled: open,
  })

  const saveMutation = useMutation({
    mutationFn: (permissionIds: string[]) => adminApi.updateRolePermissions(activeRole!.id, permissionIds),
    onSuccess: () => {
      // Two different screens cache the roles list under different keys: the Roles admin
      // page's generic CrudPage, and the role dropdown on the Users page. Refresh both.
      queryClient.invalidateQueries({ queryKey: ['admin-roles'] })
      queryClient.invalidateQueries({ queryKey: ['admin-roles-crud'] })
      showToast('success', 'Permisos actualizados.')
      onClose()
    },
    onError: (err) => {
      const message = err instanceof ApiError ? err.message : 'No se pudieron guardar los permisos.'
      setError(message)
      showToast('error', message)
    },
  })

  function toggle(name: string) {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(name)) next.delete(name)
      else next.add(name)
      return next
    })
  }

  function handleSave() {
    const permissions = permissionsQuery.data?.items ?? []
    const ids = permissions.filter((p) => selected.has(p.name)).map((p) => p.id)
    saveMutation.mutate(ids)
  }

  return (
    <Modal
      open={open}
      title={`Permisos de «${activeRole?.name}»`}
      onClose={onClose}
      size="sm"
      footer={
        <div className="flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="button" disabled={saveMutation.isPending} onClick={handleSave}>
            {saveMutation.isPending ? 'Guardando…' : 'Guardar'}
          </Button>
        </div>
      }
    >
      <div className="flex flex-col gap-2">
        {permissionsQuery.data?.items.map((permission) => (
          <label key={permission.id} className="flex items-center gap-2 text-sm text-gray-700">
            <input
              type="checkbox"
              className="accent-indigo-600"
              checked={selected.has(permission.name)}
              onChange={() => toggle(permission.name)}
            />
            {permission.name}
          </label>
        ))}
      </div>
      {error && <p className="mt-3 text-sm text-red-600">{error}</p>}
    </Modal>
  )
}
