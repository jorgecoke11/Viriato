import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Monitor, Package, Trash2, Upload } from 'lucide-react'
import type { ChangeEvent } from 'react'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { IconButton } from '../../components/ui/IconButton'
import { Modal } from '../../components/ui/Modal'
import { Skeleton } from '../../components/ui/Skeleton'
import { ApiError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import * as rpaApi from './api'
import type { DespliegueDto } from './api'
import { tamanoLegible } from './versionesDelRobot'

interface Props {
  despliegue: DespliegueDto | null
  onClose: () => void
}

/**
 * The robot's program for the launcher that runs next to Viriato. Uploading the zip of its `dotnet publish` is the whole
 * deployment: the launcher downloads it on its next round and the next copy it starts runs it. Older versions stay, to go back.
 */
export function VersionesDelRobotModal({ despliegue, onClose }: Props) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const id = despliegue?.id ?? ''

  const versionesQuery = useQuery({
    queryKey: ['versiones-de-robot', id],
    queryFn: () => rpaApi.listVersionesDeRobot(id),
    enabled: despliegue !== null,
  })

  const refrescar = () => {
    void queryClient.invalidateQueries({ queryKey: ['versiones-de-robot', id] })
    void queryClient.invalidateQueries({ queryKey: ['despliegues'] })
  }
  const error = (texto: string) => (err: unknown) => showToast('error', err instanceof ApiError ? err.message : texto)

  const subir = useMutation({
    mutationFn: (file: File) => rpaApi.subirVersionDeRobot(id, file),
    onSuccess: (version) => {
      refrescar()
      showToast('success', `Versión ${version.numero} subida. El lanzador la usará en unos segundos.`)
    },
    onError: error('No se pudo subir el robot.'),
  })

  const usar = useMutation({
    mutationFn: (versionId: string | null) => rpaApi.usarVersionDeRobot(id, versionId),
    onSuccess: (_, versionId) => {
      refrescar()
      showToast('success', versionId ? 'Hecho: la próxima copia que arranque usará esa versión.' : 'El lanzador ya no arrancará este robot.')
    },
    onError: error('No se pudo cambiar la versión.'),
  })

  const borrar = useMutation({
    mutationFn: (versionId: string) => rpaApi.borrarVersionDeRobot(id, versionId),
    onSuccess: refrescar,
    onError: error('No se pudo borrar la versión.'),
  })

  function alElegir(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (file) subir.mutate(file)
  }

  const versiones = versionesQuery.data ?? []
  const actual = versiones.find((v) => v.actual)
  const ocupado = subir.isPending || usar.isPending || borrar.isPending

  return (
    <Modal
      open={despliegue !== null}
      size="lg"
      title={despliegue ? `Robot de ${despliegue.servicioNombre} en ${despliegue.equipoNombre}` : 'Robot'}
      onClose={onClose}
      footer={
        <div className="flex flex-wrap items-center justify-between gap-2">
          {actual ? (
            <Button type="button" variant="ghost" disabled={ocupado} onClick={() => usar.mutate(null)}>
              Dejar de arrancarlo aquí
            </Button>
          ) : (
            <span />
          )}
          <Button type="button" onClick={onClose}>
            Cerrar
          </Button>
        </div>
      }
    >
      <div className="flex flex-col gap-4">
        <p className="text-sm text-gray-600">
          Sube el <span className="font-medium">.zip</span> con la salida de <code className="text-xs">dotnet publish</code> del robot. El
          lanzador de Viriato lo descarga y lo arranca cuando tenga trabajo: no hay que tocar nada en el servidor, ni claves.
        </p>

        <label
          aria-disabled={ocupado}
          className="inline-flex w-fit cursor-pointer items-center gap-2 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:brightness-110 aria-disabled:pointer-events-none aria-disabled:opacity-50"
        >
          <Upload size={15} aria-hidden="true" />
          {subir.isPending ? 'Subiendo…' : 'Subir versión'}
          <input type="file" accept=".zip,application/zip" className="hidden" onChange={alElegir} disabled={ocupado} />
        </label>

        {versionesQuery.isLoading && (
          <div role="status" aria-label="Cargando las versiones" className="flex flex-col gap-2">
            <Skeleton className="h-14 rounded-xl" />
            <Skeleton className="h-14 rounded-xl" />
          </div>
        )}
        {versionesQuery.isError && <p className="text-sm text-red-600">No se han podido cargar las versiones.</p>}
        {versionesQuery.isSuccess && versiones.length === 0 && (
          <p className="flex items-center gap-2 text-sm text-gray-500">
            <Package size={16} aria-hidden="true" />
            Aún no hay ninguna: este robot no lo arranca el lanzador de Viriato.
          </p>
        )}

        {versiones.length > 0 && (
          <ul className="flex flex-col divide-y divide-gray-100 rounded-xl border border-gray-200">
            {versiones.map((v) => (
              <li key={v.id} className="flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3">
                <div className="flex min-w-0 flex-1 flex-col gap-0.5">
                  <span className="flex items-center gap-2 text-sm font-medium text-gray-900">
                    Versión {v.numero}
                    {v.actual && <Badge tone="success">En uso</Badge>}
                    {v.pantalla && (
                      <span title="Abre un navegador: tiene su propia pantalla" className="text-gray-400">
                        <Monitor size={14} aria-label="Con navegador" />
                      </span>
                    )}
                  </span>
                  <span className="truncate text-xs text-gray-500" title={v.ejecuta}>
                    {v.nombreArchivo} · {tamanoLegible(v.tamanoBytes)} · <code>{v.ejecuta}</code>
                  </span>
                  <span className="text-xs text-gray-400">
                    {new Date(v.createdAt).toLocaleString()}
                    {v.subidaPor ? ` · ${v.subidaPor}` : ''}
                  </span>
                </div>
                {!v.actual && (
                  <div className="flex items-center gap-1">
                    <Button type="button" size="sm" variant="secondary" disabled={ocupado} onClick={() => usar.mutate(v.id)}>
                      Usar esta
                    </Button>
                    <IconButton size="sm" variant="danger" label={`Borrar la versión ${v.numero}`} disabled={ocupado} onClick={() => borrar.mutate(v.id)}>
                      <Trash2 size={15} />
                    </IconButton>
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>
    </Modal>
  )
}
