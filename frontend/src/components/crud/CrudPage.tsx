import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Inbox, Plus } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import type { AccionMasiva } from '../../lib/accionesMasivas'
import { ApiError } from '../../lib/apiClient'
import { filtrosActivos } from '../../lib/lista'
import { useToast } from '../../lib/toast/useToast'
import { useListaPaginada } from '../../lib/useListaPaginada'
import { AccionesDeSeleccion, AvisoSeleccionarTodos, InformeDeAcciones } from '../ui/AccionesDeLista'
import { Button } from '../ui/Button'
import { Card } from '../ui/Card'
import { ConfirmDialog } from '../ui/ConfirmDialog'
import { EmptyState } from '../ui/EmptyState'
import { Input } from '../ui/Input'
import { PageHeader } from '../ui/PageHeader'
import { Pagination } from '../ui/Pagination'
import { SearchField } from '../ui/SearchField'
import { SkeletonRows } from '../ui/Skeleton'
import { CrudFormModal } from './CrudFormModal'
import { CrudTable } from './CrudTable'
import type { CrudApi, CrudColumn, CrudFilterConfig, CrudFormConfig } from './types'

type ModalState<T, TFormValues> = { mode: 'create' } | { mode: 'edit'; item: T; values: TFormValues } | null

/**
 * A filter bar + table + pages + create/edit modal + delete confirmation, wired to TanStack Query and
 * parameterized by resource. Pass `form` to enable create/edit (omit it, or the corresponding
 * api.* function, for read-only resources like Permissions — see §5.1). Pass `filters` to choose
 * how the table can be searched: one general box, one control per field, or none at all. Pass
 * `acciones` to give the table a selection column (also "select all that match", past the page)
 * and a button per action; with none, there is no selection at all. The list state — search,
 * filters, page, selection — is the one every list of the app has (`useListaPaginada`).
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
  acciones = [],
  entidad = { singular: 'elemento', plural: 'elementos' },
  nombreDeFila,
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
  /** What can be done to the selected rows. Leave out for a table nobody acts on in bulk. */
  acciones?: readonly AccionMasiva[]
  /** What the rows are called, for "3 elementos seleccionados". */
  entidad?: { singular: string; plural: string }
  /** What to call a row for a screen reader when it has a tick box ("Seleccionar «Placas»"). */
  nombreDeFila?: (item: T) => string
}) {
  const [modalState, setModalState] = useState<ModalState<T, TFormValues>>(null)
  const [pendingDelete, setPendingDelete] = useState<T | null>(null)
  const [formError, setFormError] = useState<string | null>(null)
  const queryClient = useQueryClient()
  const { showToast } = useToast()

  const fuente = useListaPaginada<T>({
    clave: [resourceKey],
    obtenerId: getId,
    tamano: 25,
    cargar: ({ busqueda, filtros, pagina, tamano }) =>
      api.list({ ...filtrosActivos(filtros), ...(busqueda ? { search: busqueda } : {}), page: String(pagina), pageSize: String(tamano) }),
  })
  const conAcciones = acciones.length > 0

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
      <PageHeader
        title={title}
        actions={
          <>
            {headerExtra}
            {canCreate && (
              <Button onClick={openCreate}>
                <Plus size={16} />
                Nuevo
              </Button>
            )}
          </>
        }
      />

      <InformeDeAcciones fuente={fuente} />

      {filters.mode === 'general' && (
        <SearchField className="max-w-md" label="Buscar" placeholder={filters.placeholder ?? 'Buscar…'} value={fuente.busqueda} onChange={fuente.setBusqueda} />
      )}

      {filters.mode === 'fields' && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
          {filters.fields.map((field) =>
            field.type === 'select' ? (
              <div key={field.key} className="flex flex-col gap-1.5">
                <label htmlFor={`filter-${field.key}`} className="text-sm font-medium text-gray-700">
                  {field.label}
                </label>
                <select
                  id={`filter-${field.key}`}
                  className="field"
                  value={fuente.filtros[field.key] ?? ''}
                  onChange={(e) => fuente.ponerFiltro(field.key, e.target.value)}
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
                value={fuente.filtros[field.key] ?? ''}
                onChange={(e) => fuente.ponerFiltro(field.key, e.target.value)}
              />
            ),
          )}
        </div>
      )}

      <Card className="overflow-x-auto p-0">
        {conAcciones && <AvisoSeleccionarTodos fuente={fuente} />}
        <CrudTable
          items={fuente.filas}
          columns={columns}
          getId={getId}
          onEdit={canEdit ? openEdit : undefined}
          onDelete={api.remove ? (item) => setPendingDelete(item) : undefined}
          renderRowExtra={renderRowExtra}
          canEditRow={canEditRow}
          canDeleteRow={canDeleteRow}
          seleccion={conAcciones ? fuente.seleccion : undefined}
          nombreDeFila={nombreDeFila}
        />
        {fuente.estado.cargando && <SkeletonRows />}
        {fuente.estado.conError && (
          <div className="flex items-center justify-between gap-3 p-4 text-sm">
            <span className="text-red-600">No se han podido cargar los datos.</span>
            <Button variant="secondary" size="sm" onClick={fuente.estado.reintentar}>
              Reintentar
            </Button>
          </div>
        )}
        {fuente.estado.correcta && fuente.filas.length === 0 && (
          <EmptyState
            icon={<Inbox size={22} />}
            title="Sin resultados"
            description={
              fuente.hayFiltros
                ? 'Ningún elemento coincide con la búsqueda. Prueba con otros términos.'
                : canCreate
                  ? 'Todavía no hay nada aquí.'
                  : undefined
            }
            action={
              canCreate && !fuente.hayFiltros ? (
                <Button variant="secondary" size="sm" onClick={openCreate}>
                  <Plus size={15} />
                  Nuevo
                </Button>
              ) : undefined
            }
          />
        )}
      </Card>

      <Pagination pagina={fuente.pagina} tamano={fuente.tamano} total={fuente.total} alCambiar={fuente.setPagina} />

      {conAcciones && <AccionesDeSeleccion fuente={fuente} acciones={acciones} entidad={entidad} />}

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
