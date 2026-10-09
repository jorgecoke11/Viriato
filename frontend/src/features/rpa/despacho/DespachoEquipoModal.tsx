import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Copy, Save } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Button } from '../../../components/ui/Button'
import { Modal } from '../../../components/ui/Modal'
import { Skeleton } from '../../../components/ui/Skeleton'
import { ApiError } from '../../../lib/apiClient'
import { useToast } from '../../../lib/toast/useToast'
import * as rpaApi from '../api'
import type { DespachoEquipoDto, PlantillaDespachoDto } from '../api'
import { ColaEquipoPanel } from './ColaEquipoPanel'
import { leerTope, type DespachoConfig } from './despachoConfig'
import { DespachoConfigForm } from './DespachoConfigForm'

function desdeDespacho(d: DespachoEquipoDto): DespachoConfig {
  return {
    maxEjecucionesSimultaneas: d.maxEjecucionesSimultaneas === null ? '' : String(d.maxEjecucionesSimultaneas),
    politica: d.politica,
    orden: d.orden.map((o) => ({ id: o.servicioId, nombre: o.servicioNombre })),
  }
}

function desdePlantilla(p: PlantillaDespachoDto): DespachoConfig {
  return {
    maxEjecucionesSimultaneas: p.maxEjecucionesSimultaneas === null ? '' : String(p.maxEjecucionesSimultaneas),
    politica: p.politica,
    orden: p.orden.map((o) => ({ id: o.servicioId, nombre: o.servicioNombre })),
  }
}

/**
 * How one machine decides which of its services goes first: how many steps it runs at once, how it chooses between
 * services that all have work, and their order — set here, or copied from a template and adjusted. Next to it, what
 * the machine is doing and what waits for it, so the effect of the order is visible.
 */
export function DespachoEquipoModal({
  equipo,
  onClose,
}: {
  /** The machine being configured; null keeps the dialog closed. */
  equipo: { id: string; nombre: string } | null
  onClose: () => void
}) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const abierto = equipo !== null
  const equipoId = equipo?.id ?? ''

  const despacho = useQuery({ queryKey: ['despacho-equipo', equipoId], queryFn: () => rpaApi.getDespachoEquipo(equipoId), enabled: abierto })
  const plantillas = useQuery({ queryKey: ['plantillas-despacho'], queryFn: rpaApi.listPlantillasDespacho, enabled: abierto })

  const [config, setConfig] = useState<DespachoConfig | null>(null)
  const [plantillaId, setPlantillaId] = useState('')
  const [nombrePlantilla, setNombrePlantilla] = useState('')

  // Starts from what is saved each time the dialog opens (or its data arrives).
  useEffect(() => {
    if (abierto && despacho.data) {
      setConfig(desdeDespacho(despacho.data))
      setPlantillaId('')
      setNombrePlantilla('')
    }
    if (!abierto) setConfig(null)
  }, [abierto, despacho.data])

  const tope = config ? leerTope(config.maxEjecucionesSimultaneas) : null
  const topeValido = tope?.valido === true
  const maximo = tope?.valido ? tope.valor : null

  const guardar = useMutation({
    mutationFn: () =>
      rpaApi.updateDespachoEquipo(equipoId, {
        maxEjecucionesSimultaneas: maximo,
        politica: config!.politica,
        orden: config!.orden.map((o) => o.id),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['despacho-equipo', equipoId] })
      queryClient.invalidateQueries({ queryKey: ['cola-equipo', equipoId] })
      showToast('success', 'Despacho guardado.')
      onClose()
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo guardar el despacho.'),
  })

  const guardarComoPlantilla = useMutation({
    mutationFn: () =>
      rpaApi.createPlantillaDespacho({
        nombre: nombrePlantilla.trim(),
        descripcion: null,
        maxEjecucionesSimultaneas: maximo,
        politica: config!.politica,
        orden: config!.orden.map((o) => o.id),
      }),
    onSuccess: (creada) => {
      queryClient.invalidateQueries({ queryKey: ['plantillas-despacho'] })
      setNombrePlantilla('')
      setPlantillaId(creada.id)
      showToast('success', `Plantilla «${creada.nombre}» guardada.`)
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo guardar la plantilla.'),
  })

  const plantillaElegida = plantillas.data?.find((p) => p.id === plantillaId)
  const candidatos = (despacho.data?.serviciosDelEquipo ?? []).map((s) => ({ id: s.servicioId, nombre: s.servicioNombre }))
  const sinServicios = despacho.isSuccess && candidatos.length === 0 && (config?.orden.length ?? 0) === 0

  return (
    <Modal
      open={abierto}
      size="xl"
      title={
        <span>
          Despacho · <span className="font-semibold">{equipo?.nombre}</span>
        </span>
      }
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="button" disabled={!config || !topeValido || guardar.isPending} onClick={() => guardar.mutate()}>
            <Save size={16} />
            {guardar.isPending ? 'Guardando…' : 'Guardar'}
          </Button>
        </div>
      }
    >
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1.15fr)_minmax(0,1fr)]">
        <div className="flex flex-col gap-5">
          <p className="text-sm text-gray-600">
            Cuando los robots de esta máquina piden trabajo, no cada uno coge lo suyo: la plataforma decide quién va ahora según
            esto. Un robot que no responde, o que está apagado, no frena a los demás.
          </p>

          {despacho.isLoading || !config ? (
            <div role="status" aria-label="Cargando" className="flex flex-col gap-3">
              <Skeleton className="h-10" />
              <Skeleton className="h-24" />
              <Skeleton className="h-32" />
            </div>
          ) : despacho.isError ? (
            <p className="text-sm text-red-600">No se ha podido cargar el despacho de la máquina.</p>
          ) : (
            <>
              <DespachoConfigForm value={config} onChange={setConfig} candidatos={candidatos} />
              {sinServicios && (
                <p className="rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
                  Esta máquina no tiene despliegues todavía: crea uno en Despliegues para poder ordenar sus servicios.
                </p>
              )}

              <div className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-gray-50/60 p-3">
                <span className="text-sm font-medium text-gray-700">Plantillas</span>
                <div className="flex flex-wrap items-center gap-2">
                  <select
                    aria-label="Plantilla de despacho"
                    className="field min-w-0 flex-1 sm:max-w-xs"
                    value={plantillaId}
                    onChange={(e) => setPlantillaId(e.target.value)}
                  >
                    <option value="">Elegir una plantilla…</option>
                    {(plantillas.data ?? []).map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.nombre}
                      </option>
                    ))}
                  </select>
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={!plantillaElegida}
                    onClick={() => plantillaElegida && setConfig(desdePlantilla(plantillaElegida))}
                  >
                    <Copy size={16} />
                    Aplicar
                  </Button>
                </div>
                <p className="text-xs text-gray-500">
                  Aplicar copia los valores aquí, sin guardar todavía; después puedes ajustarlos solo para esta máquina.
                </p>
                <div className="flex flex-wrap items-center gap-2 border-t border-gray-200 pt-3">
                  <input
                    aria-label="Nombre de la nueva plantilla"
                    className="field min-w-0 flex-1 sm:max-w-xs"
                    placeholder="Guardar esto como plantilla…"
                    value={nombrePlantilla}
                    onChange={(e) => setNombrePlantilla(e.target.value)}
                  />
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={nombrePlantilla.trim() === '' || maximo === null || guardarComoPlantilla.isPending}
                    onClick={() => guardarComoPlantilla.mutate()}
                  >
                    {guardarComoPlantilla.isPending ? 'Guardando…' : 'Guardar plantilla'}
                  </Button>
                </div>
              </div>
            </>
          )}
        </div>

        <div className="rounded-xl border border-gray-200 bg-gray-50/60 p-4">
          <ColaEquipoPanel equipoId={equipoId} activo={abierto} />
        </div>
      </div>
    </Modal>
  )
}
