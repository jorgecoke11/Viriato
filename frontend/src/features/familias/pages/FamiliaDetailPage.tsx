import { useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, CircleDot, Lock, Mail, MailX, PackageCheck, PackageX } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { useCallback } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { BackLink } from '../../../components/ui/BackLink'
import { BarraDeProgreso } from '../../../components/ui/BarraDeProgreso'
import { Button } from '../../../components/ui/Button'
import { Card } from '../../../components/ui/Card'
import { ConfirmAction } from '../../../components/ui/ConfirmAction'
import { ListaDeDatos } from '../../../components/ui/ListaDeDatos'
import { StatCard } from '../../../components/ui/StatCard'
import { Timeline, TimelineItem, type TonoDeNodo } from '../../../components/ui/Timeline'
import { fechaHora } from '../../../lib/fechas'
import type { ConsultaDeLista } from '../../../lib/lista'
import { useListaPaginada } from '../../../lib/useListaPaginada'
import { useAuth } from '../../auth/useAuth'
import * as casosApi from '../../casos/api'
import type { CasoListItemDto } from '../../casos/api'
import { useColumnasDeCasos } from '../../casos/useColumnasDeCasos'
import * as api from '../api'
import type { AccionDeFamilia } from '../api'
import { EstadoDeFamiliaBadge } from '../EstadoDeFamiliaBadge'

const NODOS: Record<AccionDeFamilia, { icono: LucideIcon; tono: TonoDeNodo }> = {
  Creada: { icono: CircleDot, tono: 'indigo' },
  Cerrada: { icono: Lock, tono: 'amber' },
  Terminada: { icono: CheckCircle2, tono: 'green' },
  CierreAbierto: { icono: PackageCheck, tono: 'blue' },
  CierreNoAbierto: { icono: PackageX, tono: 'gray' },
  NotificacionEnviada: { icono: Mail, tono: 'purple' },
  NotificacionFallida: { icono: MailX, tono: 'red' },
}

const obtenerId = (c: CasoListItemDto) => c.id

/** One family: how far along it is, its cases (the same list as everywhere), what it did when it ended, and its history. */
export function FamiliaDetailPage() {
  const { id = '' } = useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { can } = useAuth()

  const familiaQuery = useQuery({
    queryKey: ['familia', id],
    queryFn: () => api.getFamilia(id),
    refetchInterval: (q) => (q.state.data && q.state.data.estado !== 'Terminada' ? 5000 : false),
  })
  const eventosQuery = useQuery({ queryKey: ['familia', id, 'eventos'], queryFn: () => api.listEventos(id), refetchInterval: 5000 })

  const cargar = useCallback(
    ({ busqueda, pagina, tamano }: ConsultaDeLista) => casosApi.listCasos({ familiaId: id, search: busqueda || undefined, page: pagina, pageSize: tamano }),
    [id],
  )
  const casos = useListaPaginada<CasoListItemDto>({
    clave: ['casos', 'familia', id],
    cargar,
    obtenerId,
    refrescarCada: (datos) => (datos?.items.some((c) => c.estado === 'EnProgreso' || c.estado === 'Pendiente') ? 3000 : false),
  })
  const columnas = useColumnasDeCasos()

  const familia = familiaQuery.data
  if (familiaQuery.isLoading) return <p className="text-gray-500">Cargando…</p>
  if (!familia) return <p className="text-gray-500">No se encontró la familia.</p>

  const refrescar = () => {
    queryClient.invalidateQueries({ queryKey: ['familia', id] })
    queryClient.invalidateQueries({ queryKey: ['familias'] })
  }

  return (
    <div className="flex flex-col gap-5">
      <BackLink to="/familias">Familias</BackLink>

      <section className="flex flex-wrap items-start justify-between gap-4 rounded-2xl border border-gray-200 bg-surface px-6 py-6 shadow-card">
        <div className="min-w-0 flex-1">
          <div className="mb-3">
            <EstadoDeFamiliaBadge estado={familia.estado} />
          </div>
          <h1 className="page-title break-words">{familia.nombre}</h1>
          <p className="mt-1 text-sm text-gray-500">
            {familia.tipo} · {familia.flujoNombre} · creada {fechaHora(familia.createdAt)}
          </p>
          {familia.estado !== 'Terminada' && (
            <div className="mt-4 max-w-md">
              <BarraDeProgreso porcentaje={api.progreso(familia)} etiqueta="Casos terminados" />
              <p className="mt-1 text-sm text-gray-600">{api.describirProgreso(familia)}</p>
            </div>
          )}
        </div>

        {familia.estado === 'Abierta' && can('casos.crear') && (
          <ConfirmAction
            disparador={(abrir) => (
              <Button variant="secondary" onClick={abrir}>
                <Lock size={16} /> Cerrar familia
              </Button>
            )}
            title="¿Cerrar la familia?"
            message="No entrarán más casos. Cuando acaben todos los que tiene, la familia terminará y hará lo que diga su tipo."
            confirmLabel="Cerrar familia"
            pendingLabel="Cerrando…"
            accion={() => api.cerrarFamilia(id)}
            mensajeDeExito="Familia cerrada"
            alTerminar={refrescar}
          />
        )}
      </section>

      {familia.estado === 'Terminada' && (
        <div className="grid gap-3 sm:grid-cols-3">
          <StatCard label="Completados" value={familia.completados ?? 0} tone="success" />
          <StatCard label="Fallidos" value={familia.fallidos ?? 0} tone={familia.fallidos ? 'danger' : 'neutral'} />
          <StatCard label="Cancelados" value={familia.cancelados ?? 0} tone="neutral" />
        </div>
      )}

      {(familia.casoDeCierreId || familia.notaDeCierre) && (
        <Card className="px-5 py-4">
          <h2 className="text-sm font-semibold text-gray-900">Caso de cierre</h2>
          {familia.casoDeCierreId ? (
            <Link to={`/casos/${familia.casoDeCierreId}`} className="mt-1 inline-block text-sm font-medium text-indigo-600 hover:text-indigo-700">
              {familia.casoDeCierreTitulo ?? 'Abrir el caso'}
            </Link>
          ) : null}
          {familia.notaDeCierre && <p className="mt-1 text-sm text-gray-600">{familia.notaDeCierre}</p>}
        </Card>
      )}

      <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <section className="flex min-w-0 flex-col gap-3">
          <h2 className="text-base font-semibold text-gray-900">Casos</h2>
          <ListaDeDatos
            fuente={casos}
            columnas={columnas}
            obtenerId={obtenerId}
            nombreDeFila={(c) => c.titulo}
            entidad={{ singular: 'caso', plural: 'casos' }}
            buscador={{ etiqueta: 'Buscar por título', placeholder: 'Buscar por título…' }}
            vacio={{ titulo: 'Sin casos todavía', descripcion: 'Los casos entran en la familia al crearse.' }}
            alHacerClic={(c) => navigate(`/casos/${c.id}`)}
            anchoMinimo="48rem"
          />
        </section>

        <Card className="h-fit px-5 py-4">
          <h2 className="mb-3 text-base font-semibold text-gray-900">Historia</h2>
          <Timeline aria-label="Historia de la familia">
            {(eventosQuery.data ?? []).map((e) => (
              <TimelineItem key={e.id} icono={NODOS[e.accion].icono} tono={NODOS[e.accion].tono} compacto>
                <p className="text-sm text-gray-800">{api.describirEvento(e)}</p>
                <p className="num text-xs text-gray-500">{fechaHora(e.occurredAt)}</p>
              </TimelineItem>
            ))}
          </Timeline>
        </Card>
      </div>
    </div>
  )
}
