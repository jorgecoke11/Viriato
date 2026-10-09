import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Save } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/apiClient'
import { useToast } from '../../../lib/toast/useToast'
import * as rpaApi from '../api'
import type { PlantillaDespachoDto } from '../api'
import { leerTope, type DespachoConfig } from './despachoConfig'
import { DespachoConfigForm } from './DespachoConfigForm'

const VACIA: DespachoConfig = { maxEjecucionesSimultaneas: '', politica: 'Prioridad', orden: [] }

/**
 * Create or edit a dispatch template: a name, and the same three decisions a machine has. The services offered are
 * all of them — a template is not tied to the machines that will use it.
 */
export function PlantillaDespachoModal({
  abierta,
  plantilla,
  onClose,
}: {
  abierta: boolean
  /** The template being edited, or null for a new one. */
  plantilla: PlantillaDespachoDto | null
  onClose: () => void
}) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [nombre, setNombre] = useState('')
  const [descripcion, setDescripcion] = useState('')
  const [config, setConfig] = useState<DespachoConfig>(VACIA)
  const [error, setError] = useState<string | null>(null)

  const servicios = useQuery({ queryKey: ['servicios-all'], queryFn: () => rpaApi.listServicios(), enabled: abierta })
  const candidatos = (servicios.data?.items ?? []).filter((s) => s.activo).map((s) => ({ id: s.id, nombre: s.nombre }))

  useEffect(() => {
    if (!abierta) return
    setError(null)
    setNombre(plantilla?.nombre ?? '')
    setDescripcion(plantilla?.descripcion ?? '')
    setConfig(
      plantilla
        ? {
            maxEjecucionesSimultaneas: plantilla.maxEjecucionesSimultaneas === null ? '' : String(plantilla.maxEjecucionesSimultaneas),
            politica: plantilla.politica,
            orden: plantilla.orden.map((o) => ({ id: o.servicioId, nombre: o.servicioNombre })),
          }
        : VACIA,
    )
  }, [abierta, plantilla])

  const tope = leerTope(config.maxEjecucionesSimultaneas)
  const maximo = tope.valido ? tope.valor : null

  const guardar = useMutation({
    mutationFn: () => {
      const input = {
        nombre: nombre.trim(),
        descripcion: descripcion.trim() || null,
        maxEjecucionesSimultaneas: maximo,
        politica: config.politica,
        orden: config.orden.map((o) => o.id),
      }
      return plantilla ? rpaApi.updatePlantillaDespacho(plantilla.id, input) : rpaApi.createPlantillaDespacho(input)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['plantillas-despacho'] })
      showToast('success', plantilla ? 'Plantilla guardada.' : 'Plantilla creada.')
      onClose()
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : 'No se pudo guardar la plantilla.'),
  })

  return (
    <Modal
      open={abierta}
      size="lg"
      title={plantilla ? `Editar plantilla · ${plantilla.nombre}` : 'Nueva plantilla de despacho'}
      onClose={onClose}
      footer={
        <div className="flex items-center justify-end gap-2">
          {error && <span className="mr-auto text-sm text-red-600">{error}</span>}
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="button" disabled={nombre.trim() === '' || maximo === null || guardar.isPending} onClick={() => guardar.mutate()}>
            <Save size={16} />
            {guardar.isPending ? 'Guardando…' : 'Guardar'}
          </Button>
        </div>
      }
    >
      <div className="flex flex-col gap-5">
        <Input label="Nombre" name="nombre" required autoComplete="off" value={nombre} onChange={(e) => setNombre(e.target.value)} />
        <Input
          label="Descripción"
          name="descripcion"
          autoComplete="off"
          value={descripcion}
          onChange={(e) => setDescripcion(e.target.value)}
          hint="Para qué sirve, p. ej. «Máquinas de oficina: el extractor primero»."
        />
        <DespachoConfigForm value={config} onChange={setConfig} candidatos={candidatos} />
      </div>
    </Modal>
  )
}
