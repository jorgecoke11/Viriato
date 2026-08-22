import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'
import { ApiError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import { Button } from '../ui/Button'
import { Card } from '../ui/Card'
import { ConfirmDialog } from '../ui/ConfirmDialog'
import { Input } from '../ui/Input'
import { CrudFormModal } from './CrudFormModal'
import { CrudTable } from './CrudTable'
import type { CrudApi, CrudColumn, CrudFormConfig } from './types'

type ModalState<T, TFormValues> = { mode: 'create' } | { mode: 'edit'; item: T; values: TFormValues } | null

/**
 * A search box + table + create/edit modal wired to TanStack Query, parameterized by resource.
 * Pass `form` to enable create/edit; omit it (or the corresponding api.* function) for read-only
 * resources like Permissions, whose source of truth is code, not the database (see §5.1).
 */
export function CrudPage<T, TFormValues extends Record<string, string>, TCreate, TUpdate>({
  title,
  resourceKey,
  getId,
  columns,
  api,
  form,
  renderRowExtra,
  headerExtra,
}: {
  title: string
  resourceKey: string
  getId: (item: T) => string
  columns: CrudColumn<T>[]
  api: CrudApi<T, TCreate, TUpdate>
  form?: CrudFormConfig<T, TFormValues, TCreate, TUpdate>
  renderRowExtra?: (item: T) => ReactNode
  headerExtra?: ReactNode
}) {
  const [search, setSearch] = useState('')
  const [modalState, setModalState] = useState<ModalState<T, TFormValues>>(null)
  const [pendingDelete, setPendingDelete] = useState<T | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const queryClient = useQueryClient()
  const { showToast } = useToast()

  const query = useQuery({
    queryKey: [resourceKey, search],
    queryFn: () => api.list(search),
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: [resourceKey] })

  const createMutation = useMutation({
    mutationFn: (input: TCreate) => api.create!(input),
    onSuccess: () => {
      setModalState(null)
      setFormError(null)
      invalidate()
      showToast('success', 'Creado correctamente.')
    },
    onError: (err) => {
      const message = err instanceof ApiError ? err.message : 'No se pudo guardar.'
      setFormError(message)
      showToast('error', message)
    },
  })

  const updateMutation = useMutation({
    mutationFn: ({ id, input }: { id: string; input: TUpdate }) => api.update!(id, input),
    onSuccess: () => {
      setModalState(null)
      setFormError(null)
      invalidate()
      showToast('success', 'Guardado correctamente.')
    },
    onError: (err) => {
      const message = err instanceof ApiError ? err.message : 'No se pudo guardar.'
      setFormError(message)
      showToast('error', message)
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.remove!(id),
    onSuccess: () => {
      setPendingDelete(null)
      invalidate()
      showToast('success', 'Eliminado correctamente.')
    },
    onError: (err) => {
      showToast('error', err instanceof ApiError ? err.message : 'No se pudo eliminar.')
      setPendingDelete(null)
    },
  })

  function handleSubmit(values: TFormValues) {
    if (!form) return
    setFormError(null)
    if (modalState?.mode === 'edit') {
      updateMutation.mutate({ id: getId(modalState.item), input: form.toUpdateInput(values) })
    } else {
      createMutation.mutate(form.toCreateInput(values))
    }
  }

  function openCreate() {
    setFormError(null)
    setModalState({ mode: 'create' })
  }

  function openEdit(item: T) {
    setFormError(null)
    setModalState({ mode: 'edit', item, values: form!.toEditValues(item) })
  }

  const canCreate = Boolean(form && api.create)
  const canEdit = Boolean(form && api.update)

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-gray-900">{title}</h1>
        <div className="flex items-center gap-2">
          {headerExtra}
          {canCreate && <Button onClick={openCreate}>Nuevo</Button>}
        </div>
      </div>

      <Input label="Buscar" name="search" value={search} onChange={(e) => setSearch(e.target.value)} />

      <Card className="overflow-x-auto p-0">
        <CrudTable
          items={query.data?.items ?? []}
          columns={columns}
          getId={getId}
          onEdit={canEdit ? openEdit : undefined}
          onDelete={api.remove ? (item) => setPendingDelete(item) : undefined}
          renderRowExtra={renderRowExtra}
        />
        {query.data?.items.length === 0 && <p className="p-4 text-sm text-gray-500">Sin resultados.</p>}
      </Card>

      {modalState && form && (
        <CrudFormModal
          title={modalState.mode === 'create' ? `Nuevo` : `Editar`}
          fields={form.fields}
          initialValues={modalState.mode === 'edit' ? modalState.values : form.emptyValues}
          onSubmit={handleSubmit}
          onClose={() => setModalState(null)}
          submitting={createMutation.isPending || updateMutation.isPending}
          error={formError}
        />
      )}

      {pendingDelete && (
        <ConfirmDialog
          title="Eliminar"
          message="¿Seguro que quieres eliminar este elemento? Esta acción no se puede deshacer."
          pending={deleteMutation.isPending}
          onConfirm={() => deleteMutation.mutate(getId(pendingDelete))}
          onCancel={() => setPendingDelete(null)}
        />
      )}
    </div>
  )
}
