import { useMutation, useQueryClient } from '@tanstack/react-query'
import { LayoutTemplate, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Badge } from '../../../components/ui/Badge'
import { Button } from '../../../components/ui/Button'
import { ConfirmDialog } from '../../../components/ui/ConfirmDialog'
import type { ColumnaDeTabla } from '../../../components/ui/DataTable'
import { IconButton } from '../../../components/ui/IconButton'
import { ListaDeDatos } from '../../../components/ui/ListaDeDatos'
import { PageHeader } from '../../../components/ui/PageHeader'
import { ApiError } from '../../../lib/apiClient'
import { paginarEnCliente } from '../../../lib/lista'
import { useToast } from '../../../lib/toast/useToast'
import { useListaPaginada } from '../../../lib/useListaPaginada'
import * as rpaApi from '../api'
import type { PlantillaDespachoDto } from '../api'
import { PlantillaDespachoModal } from '../despacho/PlantillaDespachoModal'

/** The saved dispatch setups that can be applied to machines (from the "Despacho" button of each machine). */
export function PlantillasDespachoPage() {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [editando, setEditando] = useState<{ plantilla: PlantillaDespachoDto | null } | null>(null)
  const [pendingDelete, setPendingDelete] = useState<PlantillaDespachoDto | null>(null)

  // The server hands over the whole (short) list; it is searched and cut into pages here, so it looks like every other list.
  const fuente = useListaPaginada<PlantillaDespachoDto>({
    clave: ['plantillas-despacho'],
    obtenerId: (p) => p.id,
    cargar: async (consulta) =>
      paginarEnCliente(await rpaApi.listPlantillasDespacho(), consulta, (p) => `${p.nombre} ${p.descripcion ?? ''}`),
  })

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

  const columnas: ColumnaDeTabla<PlantillaDespachoDto>[] = [
    {
      clave: 'plantilla',
      titulo: 'Plantilla',
      celda: (p) => (
        <>
          <span className="block font-medium text-gray-900">{p.nombre}</span>
          {p.descripcion && <span className="block text-xs text-gray-500">{p.descripcion}</span>}
        </>
      ),
    },
    {
      clave: 'orden',
      titulo: 'Orden de los servicios',
      celda: (p) =>
        p.orden.length === 0 ? (
          <span className="text-gray-400">Sin orden (por llegada)</span>
        ) : (
          <ol className="flex flex-wrap items-center gap-1.5">
            {p.orden.map((s, i) => (
              <li key={s.servicioId} className="inline-flex items-center gap-1.5 rounded-full bg-gray-100 py-0.5 pr-2.5 pl-1 text-xs text-gray-800">
                <span className="num flex h-5 w-5 items-center justify-center rounded-full bg-indigo-100 font-mono text-[11px] font-medium text-indigo-700">
                  {i + 1}
                </span>
                {s.servicioNombre}
              </li>
            ))}
          </ol>
        ),
    },
    {
      clave: 'limite',
      titulo: 'Límite',
      celda: (p) => <span className="num font-mono">{p.maxEjecucionesSimultaneas ?? <span className="font-sans text-gray-500">Sin límite</span>}</span>,
    },
    {
      clave: 'elige',
      titulo: 'Elige',
      celda: (p) => <Badge tone={p.politica === 'Turnos' ? 'info' : 'brand'}>{p.politica === 'Turnos' ? 'Por turnos' : 'Por prioridad'}</Badge>,
    },
    {
      clave: 'acciones',
      titulo: 'Acciones',
      celda: (p) => (
        <div className="-my-1 flex items-center gap-0.5">
          <IconButton size="sm" label="Editar" onClick={() => setEditando({ plantilla: p })}>
            <Pencil size={16} />
          </IconButton>
          <IconButton size="sm" variant="danger" label="Eliminar" onClick={() => setPendingDelete(p)}>
            <Trash2 size={16} />
          </IconButton>
        </div>
      ),
    },
  ]

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

      <ListaDeDatos
        fuente={fuente}
        columnas={columnas}
        obtenerId={(p) => p.id}
        nombreDeFila={(p) => p.nombre}
        entidad={{ singular: 'plantilla', plural: 'plantillas' }}
        buscador={{ etiqueta: 'Buscar plantillas', placeholder: 'Buscar por nombre…' }}
        vacio={{
          icono: <LayoutTemplate size={22} />,
          titulo: 'Sin plantillas',
          descripcion: 'Crea una para reutilizar el mismo orden en varias máquinas, o guarda la de una máquina desde su botón «Despacho».',
          accion: (
            <Button variant="secondary" size="sm" onClick={() => setEditando({ plantilla: null })}>
              <Plus size={15} />
              Nueva
            </Button>
          ),
        }}
        anchoMinimo="720px"
      />

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
