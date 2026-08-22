import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '../../../components/ui/Button'
import { Card } from '../../../components/ui/Card'
import { ApiError } from '../../../lib/apiClient'
import { useToast } from '../../../lib/toast/useToast'
import * as adminApi from '../api'
import type { RoleDto } from '../api'

export function RolePermissionsModal({ role, onClose }: { role: RoleDto; onClose: () => void }) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [selected, setSelected] = useState<Set<string>>(new Set(role.permissions))
  const [error, setError] = useState<string | null>(null)

  const permissionsQuery = useQuery({
    queryKey: ['admin-permissions'],
    queryFn: () => adminApi.listPermissions(''),
  })

  const saveMutation = useMutation({
    mutationFn: (permissionIds: string[]) => adminApi.updateRolePermissions(role.id, permissionIds),
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
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 px-4">
      <Card className="w-full max-w-sm">
        <h2 className="mb-4 text-lg font-medium text-gray-900">Permisos de «{role.name}»</h2>
        <div className="flex flex-col gap-2">
          {permissionsQuery.data?.items.map((permission) => (
            <label key={permission.id} className="flex items-center gap-2 text-sm text-gray-700">
              <input
                type="checkbox"
                checked={selected.has(permission.name)}
                onChange={() => toggle(permission.name)}
              />
              {permission.name}
            </label>
          ))}
        </div>
        {error && <p className="mt-3 text-sm text-red-600">{error}</p>}
        <div className="mt-4 flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="button" disabled={saveMutation.isPending} onClick={handleSave}>
            {saveMutation.isPending ? 'Guardando…' : 'Guardar'}
          </Button>
        </div>
      </Card>
    </div>
  )
}
