import { useMutation, useQueryClient } from '@tanstack/react-query'
import { CrudPage } from '../../components/crud/CrudPage'
import type { CrudColumn, CrudFormConfig } from '../../components/crud/types'
import { ApiError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import * as flujosApi from './api'
import type { FlujoEstadoDefDto } from './api'

interface EstadoFormValues {
  [key: string]: string | boolean
  codigo: string
  display: string
  orden: string
  esFinal: boolean
}

const createFields: CrudFormConfig<
  FlujoEstadoDefDto,
  EstadoFormValues,
  flujosApi.CreateFlujoEstadoRequest,
  flujosApi.UpdateFlujoEstadoRequest
>['createFields'] = [
  { name: 'codigo', label: 'Código', required: true, helpText: 'Identificador estable que usarán tus scripts. No podrá cambiarse después de crearlo.' },
  { name: 'display', label: 'Display', required: true },
  { name: 'orden', label: 'Orden', type: 'number', required: true },
  { name: 'esFinal', label: 'Es un estado final (resultado de cierre del caso)', type: 'checkbox' },
]

const editFields: CrudFormConfig<
  FlujoEstadoDefDto,
  EstadoFormValues,
  flujosApi.CreateFlujoEstadoRequest,
  flujosApi.UpdateFlujoEstadoRequest
>['editFields'] = [
  { name: 'display', label: 'Display', required: true },
  { name: 'orden', label: 'Orden', type: 'number', required: true },
  { name: 'esFinal', label: 'Es un estado final (resultado de cierre del caso)', type: 'checkbox' },
]

// Business estados are the user's own vocabulary for a process — Codigo is what a step's
// ConfiguracionJson references (immutable once created, so it's only ever in the create form),
// Display is what shows up on the Panel de casos and the Caso timeline.
export function FlujoEstadosPanel({ flujoId }: { flujoId: string }) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()

  const toggleActivo = useMutation({
    mutationFn: ({ id, activo }: { id: string; activo: boolean }) => flujosApi.updateFlujoEstado(flujoId, id, { activo }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [`flujo-${flujoId}-estados`] })
      showToast('success', 'Estado actualizado.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo actualizar el estado.'),
  })

  const columns: CrudColumn<FlujoEstadoDefDto>[] = [
    { key: 'orden', label: 'Orden', render: (e) => e.orden },
    { key: 'codigo', label: 'Código', render: (e) => <span className="font-mono text-xs">{e.codigo}</span> },
    { key: 'display', label: 'Display', render: (e) => e.display },
    {
      key: 'esFinal',
      label: 'Final',
      render: (e) =>
        e.esFinal ? (
          <span className="inline-flex items-center rounded-full bg-blue-900 px-2 py-0.5 text-xs font-medium text-white">Final</span>
        ) : null,
    },
    {
      key: 'activo',
      label: 'Activo',
      render: (e) => (
        <button
          type="button"
          className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
            e.activo ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'
          }`}
          disabled={toggleActivo.isPending}
          onClick={() => toggleActivo.mutate({ id: e.id, activo: !e.activo })}
          title={e.activo ? 'Desactivar' : 'Activar'}
        >
          {e.activo ? 'Activo' : 'Inactivo'}
        </button>
      ),
    },
  ]

  const form: CrudFormConfig<FlujoEstadoDefDto, EstadoFormValues, flujosApi.CreateFlujoEstadoRequest, flujosApi.UpdateFlujoEstadoRequest> = {
    createFields,
    editFields,
    emptyValues: { codigo: '', display: '', orden: '1', esFinal: false },
    toEditValues: (e) => ({ codigo: e.codigo, display: e.display, orden: String(e.orden), esFinal: e.esFinal }),
    toCreateInput: (values) => ({ codigo: values.codigo.trim(), display: values.display.trim(), orden: Number(values.orden) || 1, esFinal: values.esFinal }),
    toUpdateInput: (values) => ({ display: values.display.trim(), orden: Number(values.orden) || 1, esFinal: values.esFinal }),
  }

  return (
    <CrudPage<FlujoEstadoDefDto, EstadoFormValues, flujosApi.CreateFlujoEstadoRequest, flujosApi.UpdateFlujoEstadoRequest>
      title="Estados de negocio"
      resourceKey={`flujo-${flujoId}-estados`}
      getId={(e) => e.id}
      columns={columns}
      form={form}
      filters={{
        mode: 'fields',
        fields: [
          { key: 'codigo', label: 'Código', placeholder: 'EN_BANCO…' },
          {
            key: 'activo',
            label: 'Activo',
            type: 'select',
            options: [
              { value: '', label: 'Todos' },
              { value: 'true', label: 'Solo activos' },
              { value: 'false', label: 'Solo inactivos' },
            ],
          },
        ],
      }}
      api={{
        list: async (filters) => {
          const all = await flujosApi.listFlujoEstados(flujoId)
          const filtered = all
            .filter((e) => !filters.codigo || e.codigo.toLowerCase().includes(filters.codigo.toLowerCase()))
            .filter((e) => !filters.activo || String(e.activo) === filters.activo)
            .sort((a, b) => a.orden - b.orden)
          return { items: filtered, page: 1, pageSize: filtered.length || 1, total: filtered.length }
        },
        create: (input) => flujosApi.createFlujoEstado(flujoId, input),
        update: (id, input) => flujosApi.updateFlujoEstado(flujoId, id, input),
      }}
    />
  )
}
