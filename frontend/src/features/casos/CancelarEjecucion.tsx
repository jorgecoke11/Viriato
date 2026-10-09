import { useQueryClient } from '@tanstack/react-query'
import { Ban } from 'lucide-react'
import { Button } from '../../components/ui/Button'
import { ConfirmAction } from '../../components/ui/ConfirmAction'
import { IconButton } from '../../components/ui/IconButton'
import * as casosApi from './api'

/**
 * The button that cancels an execution (an RPA step waiting for a robot or being run by one), with its confirmation. It
 * cancels the Caso with it: the steps run one after another, so with this one gone there is nowhere to go. It can be a
 * button with text or just an icon, for tight rows.
 */
export function CancelarEjecucion({
  casoId,
  ejecucionPasoId,
  nombre,
  variante = 'texto',
}: {
  casoId: string
  ejecucionPasoId: string
  /** What it is called in the confirmation: the Caso's title. */
  nombre?: string
  variante?: 'texto' | 'icono'
}) {
  const queryClient = useQueryClient()

  return (
    <ConfirmAction
      disparador={(abrir) =>
        variante === 'icono' ? (
          <IconButton
            label="Cancelar ejecución"
            variant="danger"
            size="sm"
            onClick={(e) => {
              e.stopPropagation()
              abrir()
            }}
          >
            <Ban size={16} aria-hidden="true" />
          </IconButton>
        ) : (
          <Button variant="danger" size="sm" onClick={abrir}>
            <Ban size={14} aria-hidden="true" />
            Cancelar ejecución
          </Button>
        )
      }
      title="¿Cancelar esta ejecución?"
      message={`Se cancelará la ejecución${nombre ? ` de «${nombre}»` : ''} y, con ella, el caso: no seguirá adelante. Si un robot la está ejecutando no se le detiene, pero se descartará lo que informe. No se puede deshacer.`}
      confirmLabel="Cancelar ejecución"
      pendingLabel="Cancelando…"
      accion={() => casosApi.cancelarEjecucion(casoId, ejecucionPasoId)}
      mensajeDeExito="Ejecución cancelada."
      alTerminar={() => {
        for (const clave of ['caso', 'caso-ejecucion', 'caso-ejecuciones', 'cola-equipo', 'casos', 'casos-resumen']) {
          queryClient.invalidateQueries({ queryKey: [clave] })
        }
      }}
    />
  )
}
