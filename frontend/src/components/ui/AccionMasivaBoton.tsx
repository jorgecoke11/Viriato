import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import type { AccionMasiva, ResultadoMasivo } from '../../lib/accionesMasivas'
import { ApiError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import { Button } from './Button'
import { ConfirmAction } from './ConfirmAction'

/**
 * The button of a bulk action, from its definition: the label for this many selected, the confirmation (if the action asks for
 * one), the pending state, the toast, and the refresh of what changed. `alTerminar` receives the report so the screen can show
 * it. It is the same for every action of every list.
 */
export function AccionMasivaBoton({
  accion,
  ids,
  alTerminar,
}: {
  accion: AccionMasiva
  ids: readonly string[]
  alTerminar: (resultado: ResultadoMasivo) => void
}) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [ejecutando, setEjecutando] = useState(false)
  const Icono = accion.icono
  const cantidad = ids.length

  const terminar = (resultado: ResultadoMasivo) => {
    for (const clave of accion.invalidar) queryClient.invalidateQueries({ queryKey: [clave] })
    alTerminar(resultado)
  }

  const boton = (alPulsar: () => void) => (
    <Button variant={accion.peligrosa ? 'danger' : 'secondary'} size="sm" disabled={ejecutando} onClick={alPulsar}>
      <Icono size={14} aria-hidden="true" />
      {accion.etiqueta(cantidad)}
    </Button>
  )

  if (accion.confirmacion === null) {
    return boton(async () => {
      setEjecutando(true)
      try {
        const resultado = await accion.ejecutar([...ids])
        showToast('success', accion.mensajeDeExito(resultado))
        terminar(resultado)
      } catch (err) {
        showToast('error', err instanceof ApiError ? err.message : 'No se pudo completar la acción.')
      } finally {
        setEjecutando(false)
      }
    })
  }

  const { titulo, mensaje, etiquetaDeConfirmar, etiquetaPendiente, escribirDesde } = accion.confirmacion

  return (
    <ConfirmAction
      disparador={boton}
      title={titulo(cantidad)}
      message={mensaje}
      confirmLabel={etiquetaDeConfirmar}
      pendingLabel={etiquetaPendiente}
      requireText={escribirDesde && cantidad > escribirDesde.cantidad ? escribirDesde.texto : undefined}
      accion={() => accion.ejecutar([...ids])}
      mensajeDeExito={accion.mensajeDeExito}
      alTerminar={terminar}
    />
  )
}
