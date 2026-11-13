import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Monitor, Package, Trash2 } from 'lucide-react'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { IconButton } from '../../components/ui/IconButton'
import { Modal } from '../../components/ui/Modal'
import { Skeleton } from '../../components/ui/Skeleton'
import { ZonaDeSubida } from '../../components/ui/ZonaDeSubida'
import { mensajeDeError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import * as rpaApi from './api'
import { claveDeVersiones, useVersionesDelServicio } from './useVersionesDelServicio'
import { tamanoLegible } from './versionesDelRobot'

interface Servicio {
  id: string
  nombre: string
}

/**
 * The robot's program, uploaded once to its service. Every despliegue of the service that the launcher next to Viriato runs gets
 * the latest version on its next round, unless it is held at another one. Older versions stay, to go back.
 */
export function VersionesDelServicio({ servicio }: { servicio: Servicio }) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const versionesQuery = useVersionesDelServicio(servicio.id)

  const refrescar = () => {
    void queryClient.invalidateQueries({ queryKey: claveDeVersiones(servicio.id) })
    void queryClient.invalidateQueries({ queryKey: ['despliegues'] })
    void queryClient.invalidateQueries({ queryKey: ['servicios-crud'] })
  }
  const error = (texto: string) => (err: unknown) => showToast('error', mensajeDeError(err, texto))

  const subir = useMutation({
    mutationFn: (file: File) => rpaApi.subirVersionDeRobot(servicio.id, file),
    onSuccess: (version) => {
      refrescar()
      showToast(
        'success',
        version.usadaPor.length > 0
          ? `Versión ${version.numero} subida. El lanzador la usará en unos segundos.`
          : `Versión ${version.numero} subida. Ningún despliegue la arranca todavía.`,
      )
    },
    onError: error('No se pudo subir el robot.'),
  })

  const borrar = useMutation({
    mutationFn: (versionId: string) => rpaApi.borrarVersionDeRobot(servicio.id, versionId),
    onSuccess: refrescar,
    onError: error('No se pudo borrar la versión.'),
  })

  const versiones = versionesQuery.data ?? []
  const ocupado = subir.isPending || borrar.isPending

  return (
    // A zip dropped beside the zone must not make the browser open it and leave Viriato.
    <div className="flex flex-col gap-4" onDragOver={(e) => e.preventDefault()} onDrop={(e) => e.preventDefault()}>
      <p className="text-sm text-gray-600">
        Sube el <span className="font-medium">.zip</span> con la salida de <code className="text-xs">dotnet publish</code> del robot. Lo
        comparten todos los despliegues del servicio: los que arranca el lanzador de Viriato usan la última versión, salvo que los
        fijes en otra desde el despliegue.
      </p>

      <ZonaDeSubida
        texto="Elegir archivo"
        indicacion={
          <>
            Arrastra aquí el <span className="font-medium">.zip</span> del robot, o
          </>
        }
        accept=".zip,application/zip"
        extensiones={['.zip']}
        subiendo={subir.isPending}
        disabled={ocupado}
        alElegir={(file) => subir.mutate(file)}
        alRechazar={(file) => showToast('error', `«${file.name}» no es un .zip.`)}
      />

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
          Aún no hay ninguna versión de este robot.
        </p>
      )}

      {versiones.length > 0 && (
        <ul className="flex flex-col divide-y divide-gray-100 rounded-xl border border-gray-200">
          {versiones.map((v) => (
            <li key={v.id} className="flex flex-wrap items-center gap-x-4 gap-y-2 px-4 py-3">
              <div className="flex min-w-0 flex-1 flex-col gap-0.5">
                <span className="flex flex-wrap items-center gap-2 text-sm font-medium text-gray-900">
                  Versión {v.numero}
                  {v.ultima && <Badge tone="brand">Última</Badge>}
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
                {v.usadaPor.length > 0 && (
                  <span className="flex flex-wrap items-center gap-1 pt-0.5 text-xs text-gray-500">
                    En uso en
                    {v.usadaPor.map((equipo, i) => (
                      <Badge key={`${equipo}-${i}`} tone="success">
                        {equipo}
                      </Badge>
                    ))}
                  </span>
                )}
              </div>
              {v.usadaPor.length === 0 && (
                <IconButton size="sm" variant="danger" label={`Borrar la versión ${v.numero}`} disabled={ocupado} onClick={() => borrar.mutate(v.id)}>
                  <Trash2 size={15} />
                </IconButton>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

export function VersionesDelServicioModal({ servicio, onClose }: { servicio: Servicio | null; onClose: () => void }) {
  return (
    <Modal
      open={servicio !== null}
      size="lg"
      title={servicio ? `Robot de ${servicio.nombre}` : 'Robot'}
      onClose={onClose}
      footer={
        <div className="flex justify-end">
          <Button type="button" variant="ghost" onClick={onClose}>
            Cerrar
          </Button>
        </div>
      }
    >
      {servicio && <VersionesDelServicio servicio={servicio} />}
    </Modal>
  )
}
