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
import type { CrudApi, CrudColumn, CrudFilterConfig, CrudFormConfig } from './types'

type ModalState<T, TFormValues> = { mode: 'create' } | { mode: 'edit'; item: T; values: TFormValues } | null

/**
 * A filter bar + table + create/edit modal + delete confirmation, wired to TanStack Query and
 * parameterized by resource. Pass `form` to enable create/edit (omit it, or the corresponding
 * api.* function, for read-only resources like Permissions — see §5.1). Pass `filters` to choose
 * how the table can be searched: one general box, one control per field, or none at all.
 */
export function CrudPage<T, TFormValues extends Record<string, string | boolean>, TCreate, TUpdate>({
  title,
  resourceKey,
  getId,
  columns,
  api,
  form,
  filters = { mode: 'general' },
  renderRowExtra,
  headerExtra,
  deleteConfirm,
  canEditRow,
  canDeleteRow,
}: {
  title: string
  resourceKey: string
  getId: (item: T) => string
  columns: CrudColumn<T>[]
  api: CrudApi<T, TCreate, TUpdate>
  form?: CrudFormConfig<T, TFormValues, TCreate, TUpdate>
  filters?: CrudFilterConfig
  renderRowExtra?: (item: T) => ReactNode
  headerExtra?: ReactNode
  /** Customizes the delete confirmation dialog — useful when `api.remove` is really a
   * deactivate/archive action rather than a destructive delete (e.g. Users). */
  deleteConfirm?: { title?: string; message?: string; confirmLabel?: string; successMessage?: string }
  /** Hides the edit/delete action for a specific row (e.g. an admin can't edit their own user). */
  canEditRow?: (item: T) => boolean
  canDeleteRow?: (item: T) => boolean
}) {
  const [filterValues, setFilterValues] = useState<Record<string, string>>({})
  const [modalState, setModalState] = useState<ModalState<T, TFormValues>>(null)
  const [pendingDelete, setPendingDelete] = useState<T | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const queryClient = useQueryClient()
  const { showToast } = useToast()

  const query = useQuery({
    queryKey: [resourceKey, filterValues],
    queryFn: () => api.list(filterValues),
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: [resourceKey] })
  const setFilter = (key: string, value: string) => setFilterValues((prev) => ({ ...prev, [key]: value }))

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
      showToast('success', deleteConfirm?.successMessage ?? 'Eliminado correctamente.')
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
        <h1 className="text-2xl font-semibold tracking-tight text-gray-900">{title}</h1>
        <div className="flex items-center gap-2">
          {headerExtra}
          {canCreate && <Button onClick={openCreate}>Nuevo</Button>}
        </div>
      </div>

      {filters.mode === 'general' && (
        <Input
          label="Buscar"
          name="search"
          placeholder={filters.placeholder}
          value={filterValues.search ?? ''}
          onChange={(e) => setFilter('search', e.target.value)}
        />
      )}

      {filters.mode === 'fields' && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          {filters.fields.map((field) =>
            field.type === 'select' ? (
              <div key={field.key} className="flex flex-col gap-1">
                <label htmlFor={`filter-${field.key}`} className="text-sm font-medium text-gray-700">
                  {field.label}
                </label>
                <select
                  id={`filter-${field.key}`}
                  className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
                  value={filterValues[field.key] ?? ''}
                  onChange={(e) => setFilter(field.key, e.target.value)}
                >
                  {field.options?.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              </div>
            ) : (
              <Input
                key={field.key}
                label={field.label}
                name={field.key}
                placeholder={field.placeholder}
                value={filterValues[field.key] ?? ''}
                onChange={(e) => setFilter(field.key, e.target.value)}
              />
            ),
          )}
        </div>
      )}

      <Card className="overflow-x-auto p-0">
        <CrudTable
          items={query.data?.items ?? []}
          columns={columns}
          getId={getId}
          onEdit={canEdit ? openEdit : undefined}
          onDelete={api.remove ? (item) => setPendingDelete(item) : undefined}
          renderRowExtra={renderRowExtra}
          canEditRow={canEditRow}
          canDeleteRow={canDeleteRow}
        />
        {query.isLoading && <p className="p-4 text-sm text-gray-500">Cargando…</p>}
        {query.isError && (
          <div className="flex items-center justify-between p-4 text-sm">
            <span className="text-red-600">No se han podido cargar los datos.</span>
            <button className="font-medium text-gray-700 hover:text-gray-900" onClick={() => query.refetch()}>
              Reintentar
            </button>
          </div>
        )}
        {query.isSuccess && query.data.items.length === 0 && <p className="p-4 text-sm text-gray-500">Sin resultados.</p>}
      </Card>

      {form && (
        <CrudFormModal
          open={modalState !== null}
          title={modalState?.mode === 'create' ? `Nuevo` : `Editar`}
          fields={modalState?.mode === 'create' ? form.createFields : form.editFields}
          initialValues={modalState?.mode === 'edit' ? modalState.values : form.emptyValues}
          onSubmit={handleSubmit}
          onClose={() => setModalState(null)}
          submitting={createMutation.isPending || updateMutation.isPending}
          error={formError}
        />
      )}

      <ConfirmDialog
        open={pendingDelete !== null}
        title={deleteConfirm?.title ?? 'Eliminar'}
        message={deleteConfirm?.message ?? '¿Seguro que quieres eliminar este elemento? Esta acción no se puede deshacer.'}
        confirmLabel={deleteConfirm?.confirmLabel}
        pending={deleteMutation.isPending}
        onConfirm={() => pendingDelete && deleteMutation.mutate(getId(pendingDelete))}
        onCancel={() => setPendingDelete(null)}
      />
    </div>
  )
}
