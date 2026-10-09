import { useQuery } from '@tanstack/react-query'
import { useEffect, useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { Modal } from '../../components/ui/Modal'
import { TarjetasDeOpciones } from '../../components/ui/TarjetasDeOpciones'
import { useAuth } from '../auth/useAuth'
import * as casosApi from './api'
import { NuevoCasoAvanzadoModal } from './NuevoCasoAvanzadoModal'
import { NuevoCasoDesdeCreador } from './NuevoCasoDesdeCreador'
import { opcionesDeCreadores } from './opcionesDeCreadores'

/**
 * "Add a case" from a process's own card: the process is already known, so the person only picks one of its creators (or
 * nothing, if it has just one) and fills in the data. A process without creators has nothing to offer here — whoever manages it
 * is pointed to where they are configured, and can still start a case by hand with the full form.
 */
export function NuevoCasoModal({
  open,
  flujoId,
  flujoNombre,
  onClose,
}: {
  open: boolean
  flujoId: string
  flujoNombre: string
  onClose: () => void
}) {
  const navigate = useNavigate()
  const { can } = useAuth()
  const [elegidoId, setElegidoId] = useState<string | null>(null)
  const [avanzado, setAvanzado] = useState(false)

  useEffect(() => {
    if (open) {
      setElegidoId(null)
      setAvanzado(false)
    }
  }, [open])

  const query = useQuery({ queryKey: ['creadores-disponibles'], queryFn: casosApi.listCreadoresDisponibles, enabled: open })
  const creadores = useMemo(() => (query.data ?? []).filter((c) => c.flujoId === flujoId), [query.data, flujoId])
  const creador = creadores.find((c) => c.id === (elegidoId ?? (creadores.length === 1 ? creadores[0].id : null))) ?? null
  const puedeGestionar = can('casos.manage')

  if (avanzado) {
    return <NuevoCasoAvanzadoModal open={open} flujoId={flujoId} flujoNombre={flujoNombre} onClose={onClose} />
  }

  return (
    <Modal open={open} title={`Nuevo caso — ${flujoNombre}`} onClose={onClose} size={creador || creadores.length === 0 ? 'md' : 'lg'}>
      <div className="flex flex-col gap-4">
        {query.isLoading ? (
          <p className="text-gray-500">Cargando…</p>
        ) : creadores.length === 0 ? (
          <div className="flex flex-col gap-3 text-sm text-gray-600">
            <p>Este proceso todavía no tiene creadores de caso, así que no hay nada que ofrecer aquí.</p>
            {puedeGestionar && (
              <p className="flex flex-wrap gap-x-4 gap-y-1">
                <Link to={`/admin/flujos/${flujoId}`} className="font-medium text-indigo-600 hover:text-indigo-800" onClick={onClose}>
                  Configurar sus creadores
                </Link>
                <button type="button" className="font-medium text-indigo-600 hover:text-indigo-800" onClick={() => setAvanzado(true)}>
                  Crear un caso sin creador
                </button>
              </p>
            )}
          </div>
        ) : (
          <>
            {creadores.length > 1 && (
              <TarjetasDeOpciones etiqueta="Qué crear" opciones={opcionesDeCreadores(creadores)} valor={creador?.id ?? null} alCambiar={setElegidoId} />
            )}
            {creador && (
              <NuevoCasoDesdeCreador
                key={creador.id}
                creador={creador}
                alCancelar={onClose}
                alCrear={(caso) => {
                  onClose()
                  navigate(`/casos/${caso.id}`)
                }}
              />
            )}
          </>
        )}
      </div>
    </Modal>
  )
}
