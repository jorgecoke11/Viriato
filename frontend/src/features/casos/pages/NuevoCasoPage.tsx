import { useQuery } from '@tanstack/react-query'
import { FilePlus2, Settings2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { BackLink } from '../../../components/ui/BackLink'
import { Card } from '../../../components/ui/Card'
import { EmptyState } from '../../../components/ui/EmptyState'
import { TarjetasDeOpciones } from '../../../components/ui/TarjetasDeOpciones'
import { useAuth } from '../../auth/useAuth'
import * as casosApi from '../api'
import { NuevoCasoDesdeCreador } from '../NuevoCasoDesdeCreador'
import { opcionesDeCreadores } from '../opcionesDeCreadores'

export function NuevoCasoPage() {
  const navigate = useNavigate()
  const { can } = useAuth()
  const [searchParams] = useSearchParams()
  // Arriving from a process's own "Añadir caso" keeps only that process's creators.
  const flujoId = searchParams.get('flujoId')

  const query = useQuery({ queryKey: ['creadores-disponibles'], queryFn: casosApi.listCreadoresDisponibles })
  const creadores = useMemo(() => (query.data ?? []).filter((c) => !flujoId || c.flujoId === flujoId), [query.data, flujoId])

  const [elegidoId, setElegidoId] = useState<string | null>(null)
  // With a single creator there is nothing to choose.
  const creador = creadores.find((c) => c.id === (elegidoId ?? (creadores.length === 1 ? creadores[0].id : null))) ?? null

  const puedeGestionar = can('casos.manage')

  return (
    <div className="flex flex-col gap-4">
      <BackLink to="/casos/lista">Listado</BackLink>

      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="page-title">Nuevo caso</h1>
          <p className="text-sm text-gray-500">
            {creador ? `Con «${creador.nombre}».` : 'Elige qué quieres crear. Lo demás ya viene configurado.'}
          </p>
        </div>
        {puedeGestionar && (
          <Link to="/casos/nuevo/avanzado" className="inline-flex items-center gap-1.5 text-sm font-medium text-gray-500 hover:text-gray-900">
            <Settings2 size={15} aria-hidden="true" />
            Crear sin creador (avanzado)
          </Link>
        )}
      </div>

      {query.isLoading ? (
        <p className="text-gray-500">Cargando…</p>
      ) : creadores.length === 0 ? (
        <EmptyState
          icon={<FilePlus2 size={22} aria-hidden="true" />}
          title="No hay nada que crear todavía"
          description="Aún no hay creadores de caso para tus procesos. Quien administra el proceso los configura en su pestaña «Creadores»."
        />
      ) : (
        <>
          {creadores.length > 1 && (
            <TarjetasDeOpciones
              etiqueta="Qué crear"
              opciones={opcionesDeCreadores(creadores)}
              valor={creador?.id ?? null}
              alCambiar={setElegidoId}
            />
          )}

          {creador && (
            <Card className="max-w-2xl">
              <NuevoCasoDesdeCreador key={creador.id} creador={creador} alCrear={(caso) => navigate(`/casos/${caso.id}`)} />
            </Card>
          )}
        </>
      )}
    </div>
  )
}
