import { useState, type ReactNode } from 'react'
import { ApiError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import { ConfirmDialog } from './ConfirmDialog'

interface Props<R> {
  /** Draws whatever opens the confirmation (a button, an icon, a menu item); call `abrir` from its click. */
  disparador: (abrir: () => void) => ReactNode
  title: string
  message: string
  confirmLabel: string
  pendingLabel: string
  /** The action itself. The dialog stays open and shows `pendingLabel` until it settles. */
  accion: () => Promise<R>
  /** The toast on success: a text, or one made from what the action returned. */
  mensajeDeExito: string | ((resultado: R) => string)
  /** The toast on failure, if the server did not give a reason of its own. */
  mensajeDeError?: string
  /** For what is big or cannot be undone: the word to type to be able to confirm. */
  requireText?: string
  /** Runs after a success: refresh what changed, clear a selection… */
  alTerminar?: (resultado: R) => void
}

/**
 * "Are you sure?" around any action: a trigger of your own, a confirmation dialog, the pending state, and the toast with the
 * outcome, success or failure. Whoever uses it only says what the action is and what to refresh afterwards.
 */
export function ConfirmAction<R>({
  disparador,
  title,
  message,
  confirmLabel,
  pendingLabel,
  accion,
  mensajeDeExito,
  mensajeDeError = 'No se pudo completar la acción.',
  requireText,
  alTerminar,
}: Props<R>) {
  const { showToast } = useToast()
  const [abierto, setAbierto] = useState(false)
  const [pendiente, setPendiente] = useState(false)

  async function confirmar() {
    setPendiente(true)
    try {
      const resultado = await accion()
      setAbierto(false)
      showToast('success', typeof mensajeDeExito === 'function' ? mensajeDeExito(resultado) : mensajeDeExito)
      alTerminar?.(resultado)
    } catch (err) {
      showToast('error', err instanceof ApiError ? err.message : mensajeDeError)
    } finally {
      setPendiente(false)
    }
  }

  return (
    <>
      {disparador(() => setAbierto(true))}
      <ConfirmDialog
        open={abierto}
        title={title}
        message={message}
        confirmLabel={confirmLabel}
        pendingLabel={pendingLabel}
        pending={pendiente}
        requireText={requireText}
        onConfirm={confirmar}
        onCancel={() => !pendiente && setAbierto(false)}
      />
    </>
  )
}
