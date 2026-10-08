import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { LayoutTemplate, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Badge } from '../../../components/ui/Badge'
import { Button } from '../../../components/ui/Button'
import { Card } from '../../../components/ui/Card'
import { ConfirmDialog } from '../../../components/ui/ConfirmDialog'
import { EmptyState } from '../../../components/ui/EmptyState'
import { IconButton } from '../../../components/ui/IconButton'
import { PageHeader } from '../../../components/ui/PageHeader'
import { SkeletonRows } from '../../../components/ui/Skeleton'
import { ApiError } from '../../../lib/apiClient'
import { useToast } from '../../../lib/toast/useToast'
import * as rpaApi from '../api'
import type { PlantillaDespachoDto } from '../api'
import { PlantillaDespachoModal } from '../despacho/PlantillaDespachoModal'

/** The saved dispatch setups that can be applied to machines (from the "Despacho" button of each machine). */
export function PlantillasDespachoPage() {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [editando, setEditando] = useState<{ plantilla: PlantillaDespachoDto | null } | null>(null)
  const [pendingDelete, setPendingDelete] = useState<PlantillaDespachoDto | null>(null)

  const plantillas = useQuery({ queryKey: ['plantillas-despacho'], queryFn: rpaApi.listPlantillasDespacho })

  const eliminar = useMutation({
    mutationFn: (id: string) => rpaApi.deletePlantillaDespacho(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['plantillas-despacho'] })
      setPendingDelete(null)
      showToast('success', 'Plantilla eliminada. Las máquinas que la usaron conservan su configuración.')
    },
    onError: (err) => {
      setPendingDelete(null)
      showToast('error', err instanceof ApiError ? err.message : 'No se pudo eliminar la plantilla.')
    },
  })

  const lista = plantillas.data ?? []

  return (
    <div className="flex flex-col gap-4">
      <PageHeader
        title="Plantillas de despacho"
        description="Un orden de servicios, un límite de ejecuciones simultáneas y una forma de elegir, guardados para aplicarlos a varias máquinas. Aplicar una plantilla copia sus valores: cada máquina puede cambiarlos después sin tocar la plantilla."
        actions={
          <Button onClick={() => setEditando({ plantilla: null })}>
            <Plus size={16} />
            Nueva
          </Button>
        }
      />

      <Card className="overflow-x-auto p-0">
        <table className="w-full min-w-[720px] text-left text-sm">
          <thead className="border-b border-gray-200 bg-gray-50/70">
            <tr>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">Plantilla</th>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">Orden de los servicios</th>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">A la vez</th>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">Elige</th>
              <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">Acciones</th>
            </tr>
          </thead>
          <tbody>
            {lista.map((plantilla) => (
              <tr key={plantilla.id} className="border-b border-gray-100 last:border-0 hover:bg-gray-50/70">
                <td className="px-4 py-3">
                  <span className="block font-medium text-gray-900">{plantilla.nombre}</span>
                  {plantilla.descripcion && <span className="block text-xs text-gray-500">{plantilla.descripcion}</span>}
                </td>
                <td className="px-4 py-3">
                  {plantilla.orden.length === 0 ? (
                    <span className="text-gray-400">Sin orden (por llegada)</span>
                  ) : (
                    <ol className="flex flex-wrap items-center gap-1.5">
                      {plantilla.orden.map((s, i) => (
                        <li key={s.servicioId} className="inline-flex items-center gap-1.5 rounded-full bg-gray-100 py-0.5 pr-2.5 pl-1 text-xs text-gray-800">
                          <span className="num flex h-5 w-5 items-center justify-center rounded-full bg-indigo-100 font-mono text-[11px] font-medium text-indigo-700">
                            {i + 1}
                          </span>
                          {s.servicioNombre}
                        </li>
                      ))}
                    </ol>
                  )}
                </td>
                <td className="num px-4 py-3 font-mono">{plantilla.maxEjecucionesSimultaneas}</td>
                <td className="px-4 py-3">
                  <Badge tone={plantilla.politica === 'Turnos' ? 'info' : 'brand'}>
                    {plantilla.politica === 'Turnos' ? 'Por turnos' : 'Por prioridad'}
                  </Badge>
                </td>
                <td className="px-4 py-2">
                  <div className="flex items-center gap-0.5">
                    <IconButton size="sm" label="Editar" onClick={() => setEditando({ plantilla })}>
                      <Pencil size={16} />
                    </IconButton>
                    <IconButton size="sm" variant="danger" label="Eliminar" onClick={() => setPendingDelete(plantilla)}>
                      <Trash2 size={16} />
                    </IconButton>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {plantillas.isLoading && <SkeletonRows />}
        {plantillas.isError && <p className="p-4 text-sm text-red-600">No se han podido cargar las plantillas.</p>}
        {plantillas.isSuccess && lista.length === 0 && (
          <EmptyState
            icon={<LayoutTemplate size={22} />}
            title="Sin plantillas"
            description="Crea una para reutilizar el mismo orden en varias máquinas, o guarda la de una máquina desde su botón «Despacho»."
            action={
              <Button variant="secondary" size="sm" onClick={() => setEditando({ plantilla: null })}>
                <Plus size={15} />
                Nueva
              </Button>
            }
          />
        )}
      </Card>

      <PlantillaDespachoModal abierta={editando !== null} plantilla={editando?.plantilla ?? null} onClose={() => setEditando(null)} />

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Eliminar plantilla"
        message="¿Seguro que quieres eliminar esta plantilla? Las máquinas a las que se aplicó conservan su configuración."
        pending={eliminar.isPending}
        onConfirm={() => pendingDelete && eliminar.mutate(pendingDelete.id)}
        onCancel={() => setPendingDelete(null)}
      />
    </div>
  )
}
