import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '../../../components/ui/Button'
import { Card } from '../../../components/ui/Card'
import { Input } from '../../../components/ui/Input'
import { ApiError } from '../../../lib/apiClient'
import { useToast } from '../../../lib/toast/useToast'
import { useAuth } from '../../auth/useAuth'
import * as adminApi from '../api'

export function UsersPage() {
  const { can, user: currentUser } = useAuth()
  const [search, setSearch] = useState('')
  const queryClient = useQueryClient()
  const { showToast } = useToast()

  const usersQuery = useQuery({
    queryKey: ['admin-users', search],
    queryFn: () => adminApi.listUsers({ search }),
  })

  const rolesQuery = useQuery({
    queryKey: ['admin-roles'],
    queryFn: () => adminApi.listRoles(),
  })

  const assignMutation = useMutation({
    mutationFn: ({ userId, roleId }: { userId: string; roleId: string }) => adminApi.assignRole(userId, roleId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-users'] })
      showToast('success', 'Rol asignado.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo asignar el rol.'),
  })

  const removeMutation = useMutation({
    mutationFn: ({ userId, roleId }: { userId: string; roleId: string }) => adminApi.removeRole(userId, roleId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-users'] })
      showToast('success', 'Rol retirado.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo quitar el rol.'),
  })

  if (!can('users.manage')) {
    return <p className="text-gray-600">No tienes permiso para ver esta sección.</p>
  }

  const roles = rolesQuery.data?.items ?? []

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-2xl font-semibold tracking-tight text-gray-900">Usuarios</h1>

      <Input
        label="Buscar"
        name="search"
        placeholder="Email o nombre…"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
      />

      <Card className="overflow-x-auto p-0">
        <table className="w-full min-w-[640px] text-left text-sm">
          <thead className="border-b border-gray-200 text-gray-500">
            <tr>
              <th className="px-4 py-3 font-medium">Usuario</th>
              <th className="px-4 py-3 font-medium">Roles</th>
              <th className="px-4 py-3 font-medium">Asignar rol</th>
            </tr>
          </thead>
          <tbody>
            {usersQuery.data?.items.map((user) => {
              const isSelf = user.id === currentUser?.id
              const assignableRoles = roles.filter((role) => !user.roles.includes(role.name))

              return (
                <tr key={user.id} className="border-b border-gray-100 last:border-0">
                  <td className="px-4 py-3">
                    <div className="font-medium text-gray-900">{user.displayName}</div>
                    <div className="text-gray-500">{user.email}</div>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap gap-1">
                      {user.roles.map((roleName) => {
                        const role = roles.find((r) => r.name === roleName)
                        return (
                          <span
                            key={roleName}
                            className="inline-flex items-center gap-1 rounded-full bg-gray-100 px-2 py-1 text-xs text-gray-700"
                          >
                            {roleName}
                            {role && !isSelf && (
                              <button
                                type="button"
                                className="text-gray-400 hover:text-red-600"
                                disabled={removeMutation.isPending}
                                onClick={() => removeMutation.mutate({ userId: user.id, roleId: role.id })}
                              >
                                ×
                              </button>
                            )}
                          </span>
                        )
                      })}
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    {isSelf ? (
                      <span className="text-gray-400">—</span>
                    ) : (
                      <RoleAssigner
                        roles={assignableRoles}
                        disabled={assignMutation.isPending}
                        onAssign={(roleId) => assignMutation.mutate({ userId: user.id, roleId })}
                      />
                    )}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
        {usersQuery.data?.items.length === 0 && <p className="p-4 text-sm text-gray-500">No se encontraron usuarios.</p>}
      </Card>
    </div>
  )
}

function RoleAssigner({
  roles,
  disabled,
  onAssign,
}: {
  roles: adminApi.RoleDto[]
  disabled: boolean
  onAssign: (roleId: string) => void
}) {
  const [selected, setSelected] = useState('')

  if (roles.length === 0) {
    return <span className="text-gray-400">—</span>
  }

  return (
    <div className="flex gap-2">
      <select
        className="rounded-lg border border-gray-300 px-2 py-1 text-sm"
        value={selected}
        onChange={(e) => setSelected(e.target.value)}
      >
        <option value="">Seleccionar…</option>
        {roles.map((role) => (
          <option key={role.id} value={role.id}>
            {role.name}
          </option>
        ))}
      </select>
      <Button
        variant="ghost"
        disabled={!selected || disabled}
        onClick={() => {
          onAssign(selected)
          setSelected('')
        }}
      >
        Asignar
      </Button>
    </div>
  )
}
