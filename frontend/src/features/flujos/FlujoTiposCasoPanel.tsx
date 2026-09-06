import { useMutation, useQueryClient } from '@tanstack/react-query'
import { CrudPage } from '../../components/crud/CrudPage'
import type { CrudColumn, CrudFormConfig } from '../../components/crud/types'
import { ApiError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import * as flujosApi from './api'
import type { FlujoTipoCasoDefDto } from './api'

interface TipoFormValues {
  [key: string]: string | boolean
  nombre: string
  orden: string
}

const fields: CrudFormConfig<
  FlujoTipoCasoDefDto,
  TipoFormValues,
  flujosApi.CreateFlujoTipoCasoRequest,
  flujosApi.UpdateFlujoTipoCasoRequest
>['createFields'] = [
  { name: 'nombre', label: 'Nombre', required: true },
  { name: 'orden', label: 'Orden', type: 'number', required: true },
]

// Categories the user defines per process to classify its Casos (e.g. "Alta simple" vs "Alta
// compleja") — chosen once when a Caso is created, orthogonal to its business estado.
export function FlujoTiposCasoPanel({ flujoId }: { flujoId: string }) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()

  const toggleActivo = useMutation({
    mutationFn: ({ id, activo }: { id: string; activo: boolean }) => flujosApi.updateFlujoTipoCaso(flujoId, id, { activo }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: [`flujo-${flujoId}-tipos-caso`] })
      showToast('success', 'Tipo de caso actualizado.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo actualizar el tipo de caso.'),
  })

  const columns: CrudColumn<FlujoTipoCasoDefDto>[] = [
    { key: 'orden', label: 'Orden', render: (t) => t.orden },
    { key: 'nombre', label: 'Nombre', render: (t) => t.nombre },
    {
      key: 'activo',
      label: 'Activo',
      render: (t) => (
        <button
          type="button"
          className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${
            t.activo ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'
          }`}
          disabled={toggleActivo.isPending}
          onClick={() => toggleActivo.mutate({ id: t.id, activo: !t.activo })}
          title={t.activo ? 'Desactivar' : 'Activar'}
        >
          {t.activo ? 'Activo' : 'Inactivo'}
        </button>
      ),
    },
  ]

  const form: CrudFormConfig<FlujoTipoCasoDefDto, TipoFormValues, flujosApi.CreateFlujoTipoCasoRequest, flujosApi.UpdateFlujoTipoCasoRequest> = {
    createFields: fields,
    editFields: fields,
    emptyValues: { nombre: '', orden: '1' },
    toEditValues: (t) => ({ nombre: t.nombre, orden: String(t.orden) }),
    toCreateInput: (values) => ({ nombre: values.nombre.trim(), orden: Number(values.orden) || 1 }),
    toUpdateInput: (values) => ({ nombre: values.nombre.trim(), orden: Number(values.orden) || 1 }),
  }

  return (
    <CrudPage<FlujoTipoCasoDefDto, TipoFormValues, flujosApi.CreateFlujoTipoCasoRequest, flujosApi.UpdateFlujoTipoCasoRequest>
      title="Tipos de caso"
      resourceKey={`flujo-${flujoId}-tipos-caso`}
      getId={(t) => t.id}
      columns={columns}
      form={form}
      filters={{ mode: 'fields', fields: [{ key: 'nombre', label: 'Nombre', placeholder: 'Alta simple…' }] }}
      api={{
        list: async (filters) => {
          const all = await flujosApi.listFlujoTiposCaso(flujoId)
          const filtered = all
            .filter((t) => !filters.nombre || t.nombre.toLowerCase().includes(filters.nombre.toLowerCase()))
            .sort((a, b) => a.orden - b.orden)
          return { items: filtered, page: 1, pageSize: filtered.length || 1, total: filtered.length }
        },
        create: (input) => flujosApi.createFlujoTipoCaso(flujoId, input),
        update: (id, input) => flujosApi.updateFlujoTipoCaso(flujoId, id, input),
      }}
    />
  )
}
