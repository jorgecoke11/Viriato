import { useQueryClient } from '@tanstack/react-query'
import { Check, X } from 'lucide-react'
import { useId, useState } from 'react'
import { Button } from '../../components/ui/Button'
import { ConfirmAction } from '../../components/ui/ConfirmAction'
import * as casosApi from './api'

/**
 * The decision a person takes on a step that waits for a human review: approve it (the Caso goes on) or reject it (it does not), with a
 * comment if they want. Each decision asks to be confirmed, because the Caso moves on from it. The comment is shared by both buttons.
 */
export function RevisarPaso({ ejecucionPasoId, nombre }: { ejecucionPasoId: string; nombre: string }) {
  const queryClient = useQueryClient()
  const [comentario, setComentario] = useState('')
  const idComentario = useId()

  const refrescar = () => {
    for (const clave of ['caso', 'caso-ejecucion', 'caso-ejecuciones', 'caso-timeline', 'casos', 'casos-resumen']) {
      queryClient.invalidateQueries({ queryKey: [clave] })
    }
  }
  const comentarioLimpio = comentario.trim() === '' ? undefined : comentario.trim()

  return (
    <div className="mt-3 flex flex-col gap-3">
      <div className="flex flex-col gap-1.5">
        <label htmlFor={idComentario} className="text-xs font-medium text-gray-600">
          Comentario (opcional)
        </label>
        <textarea
          id={idComentario}
          rows={2}
          maxLength={1000}
          className="field"
          value={comentario}
          onChange={(e) => setComentario(e.target.value)}
          placeholder="Por qué apruebas o rechazas."
        />
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <ConfirmAction
          disparador={(abrir) => (
            <Button onClick={abrir}>
              <Check size={16} aria-hidden="true" />
              Aprobar
            </Button>
          )}
          title="¿Aprobar este paso?"
          message={`Se aprobará «${nombre}» y el caso seguirá con el paso siguiente.`}
          confirmLabel="Aprobar"
          pendingLabel="Aprobando…"
          accion={() => casosApi.resolverRevision(ejecucionPasoId, 'Aprobada', comentarioLimpio)}
          mensajeDeExito="Paso aprobado."
          alTerminar={refrescar}
        />
        <ConfirmAction
          disparador={(abrir) => (
            <Button variant="danger" onClick={abrir}>
              <X size={16} aria-hidden="true" />
              Rechazar
            </Button>
          )}
          title="¿Rechazar este paso?"
          message={`Se rechazará «${nombre}»: el caso no seguirá adelante por esta vía.`}
          confirmLabel="Rechazar"
          pendingLabel="Rechazando…"
          accion={() => casosApi.resolverRevision(ejecucionPasoId, 'Rechazada', comentarioLimpio)}
          mensajeDeExito="Paso rechazado."
          alTerminar={refrescar}
        />
      </div>
    </div>
  )
}
