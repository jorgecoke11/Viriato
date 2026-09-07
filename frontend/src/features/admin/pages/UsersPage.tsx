import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { CrudPage } from '../../../components/crud/CrudPage'
import type { CrudColumn, CrudFormConfig } from '../../../components/crud/types'
import { Button } from '../../../components/ui/Button'
import { ApiError } from '../../../lib/apiClient'
import { useToast } from '../../../lib/toast/useToast'
import type { UserDto } from '../../auth/types'
import { useAuth } from '../../auth/useAuth'
import * as flujosApi from '../../flujos/api'
import * as adminApi from '../api'

interface UserFormValues {
  [key: string]: string | boolean
  email: string
  password: string
  displayName: string
  isActive: boolean
}

const form: CrudFormConfig<UserDto, UserFormValues, adminApi.CreateUserInput, adminApi.UpdateUserInput> = {
  createFields: [
    { name: 'email', label: 'Email', required: true },
    { name: 'password', label: 'Contraseña', type: 'password', required: true, helpText: 'Mínimo 12 caracteres.' },
    { name: 'displayName', label: 'Nombre', required: true },
  ],
  editFields: [
    { name: 'displayName', label: 'Nombre', required: true },
    { name: 'isActive', label: 'Activo', type: 'checkbox' },
  ],
  emptyValues: { email: '', password: '', displayName: '', isActive: true },
  toEditValues: (user) => ({ email: user.email, password: '', displayName: user.displayName, isActive: user.isActive }),
  toCreateInput: (values) => ({ email: values.email, password: values.password, displayName: values.displayName }),
  toUpdateInput: (values) => ({ displayName: values.displayName, isActive: values.isActive }),
}

export function UsersPage() {
  const { can, user: currentUser } = useAuth()
  const queryClient = useQueryClient()
  const { showToast } = useToast()

  const rolesQuery = useQuery({
    queryKey: ['admin-roles'],
    queryFn: () => adminApi.listRoles(),
  })

  const flujosQuery = useQuery({
    queryKey: ['admin-flujos-all'],
    queryFn: () => flujosApi.listFlujos(),
    enabled: can('flujos.manage'),
  })

  const assignMutation = useMutation({
    mutationFn: ({ userId, roleId }: { userId: string; roleId: string }) => adminApi.assignRole(userId, roleId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-users-crud'] })
      showToast('success', 'Rol asignado.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo asignar el rol.'),
  })

  const removeMutation = useMutation({
    mutationFn: ({ userId, roleId }: { userId: string; roleId: string }) => adminApi.removeRole(userId, roleId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin-users-crud'] })
      showToast('success', 'Rol retirado.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo quitar el rol.'),
  })

  if (!can('users.manage')) {
    return <p className="text-gray-600">No tienes permiso para ver esta sección.</p>
  }

  const roles = rolesQuery.data?.items ?? []
  const canManageFlujos = can('flujos.manage')
  const allFlujos = flujosQuery.data?.items ?? []
  const isSelf = (user: UserDto) => user.id === currentUser?.id

  const columns: CrudColumn<UserDto>[] = [
    {
      key: 'user',
      label: 'Usuario',
      render: (user) => (
        <>
          <div className="font-medium text-gray-900">{user.displayName}</div>
          <div className="text-gray-500">{user.email}</div>
        </>
      ),
    },
    {
      key: 'status',
      label: 'Estado',
      render: (user) => (
        <span
          className={`rounded-full px-2 py-0.5 text-xs ${
            user.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'
          }`}
        >
          {user.isActive ? 'Activo' : 'Inactivo'}
        </span>
      ),
    },
    {
      key: 'roles',
      label: 'Roles',
      render: (user) => (
        <div className="flex flex-wrap gap-1">
          {user.roles.map((roleName) => {
            const role = roles.find((r) => r.name === roleName)
            return (
              <span
                key={roleName}
                className="inline-flex items-center gap-1 rounded-full bg-gray-100 px-2 py-1 text-xs text-gray-700"
              >
                {roleName}
                {role && !isSelf(user) && (
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
      ),
    },
    {
      key: 'assign',
      label: 'Asignar rol',
      render: (user) => {
        if (isSelf(user)) {
          return <span className="text-gray-400">—</span>
        }
        const assignableRoles = roles.filter((role) => !user.roles.includes(role.name))
        return (
          <RoleAssigner
            roles={assignableRoles}
            disabled={assignMutation.isPending}
            onAssign={(roleId) => assignMutation.mutate({ userId: user.id, roleId })}
          />
        )
      },
    },
    ...(canManageFlujos
      ? [
          {
            key: 'procesos',
            label: 'Procesos',
            render: (user) => <UserProcesosCell userId={user.id} allFlujos={allFlujos} />,
          } satisfies CrudColumn<UserDto>,
        ]
      : []),
  ]

  return (
    <CrudPage<UserDto, UserFormValues, adminApi.CreateUserInput, adminApi.UpdateUserInput>
      title="Usuarios"
      resourceKey="admin-users-crud"
      getId={(user) => user.id}
      columns={columns}
      form={form}
      filters={{ mode: 'general', placeholder: 'Email o nombre…' }}
      api={{
        list: adminApi.listUsers,
        create: adminApi.createUser,
        update: adminApi.updateUser,
        remove: adminApi.deactivateUser,
      }}
      canEditRow={(user) => !isSelf(user)}
      canDeleteRow={(user) => !isSelf(user)}
      deleteConfirm={{
        title: 'Desactivar usuario',
        message: '¿Seguro que quieres desactivar a este usuario? Perderá acceso a la aplicación.',
        confirmLabel: 'Desactivar',
        successMessage: 'Usuario desactivado.',
      }}
    />
  )
}

function UserProcesosCell({ userId, allFlujos }: { userId: string; allFlujos: flujosApi.FlujoDto[] }) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [selected, setSelected] = useState('')

  const assignedQuery = useQuery({
    queryKey: ['admin-user-flujos', userId],
    queryFn: () => adminApi.listFlujosAsignadosDeUsuario(userId),
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['admin-user-flujos', userId] })

  const assignMutation = useMutation({
    mutationFn: (flujoId: string) => adminApi.asignarFlujo(flujoId, userId),
    onSuccess: () => {
      invalidate()
      showToast('success', 'Proceso asignado.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo asignar el proceso.'),
  })

  const removeMutation = useMutation({
    mutationFn: (flujoId: string) => adminApi.desasignarFlujo(flujoId, userId),
    onSuccess: () => {
      invalidate()
      showToast('success', 'Proceso retirado.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo quitar el proceso.'),
  })

  if (assignedQuery.isLoading) {
    return <span className="text-gray-400">Cargando…</span>
  }

  const assigned = assignedQuery.data ?? []
  const assignedIds = new Set(assigned.map((flujo) => flujo.id))
  const assignable = allFlujos.filter((flujo) => !assignedIds.has(flujo.id))

  return (
    <div className="flex flex-col gap-1">
      <div className="flex flex-wrap gap-1">
        {assigned.length === 0 && <span className="text-gray-400">—</span>}
        {assigned.map((flujo) => (
          <span
            key={flujo.id}
            className="inline-flex items-center gap-1 rounded-full bg-gray-100 px-2 py-1 text-xs text-gray-700"
          >
            {flujo.nombre}
            <button
              type="button"
              className="text-gray-400 hover:text-red-600"
              disabled={removeMutation.isPending}
              onClick={() => removeMutation.mutate(flujo.id)}
            >
              ×
            </button>
          </span>
        ))}
      </div>
      {assignable.length > 0 && (
        <div className="flex gap-2">
          <select
            className="rounded-lg border border-gray-300 px-2 py-1 text-sm"
            value={selected}
            onChange={(e) => setSelected(e.target.value)}
          >
            <option value="">Asignar proceso…</option>
            {assignable.map((flujo) => (
              <option key={flujo.id} value={flujo.id}>
                {flujo.nombre}
              </option>
            ))}
          </select>
          <Button
            variant="ghost"
            disabled={!selected || assignMutation.isPending}
            onClick={() => {
              assignMutation.mutate(selected)
              setSelected('')
            }}
          >
            Asignar
          </Button>
        </div>
      )}
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
