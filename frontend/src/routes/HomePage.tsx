import { useQuery } from '@tanstack/react-query'
import { ArrowRight, CircleAlert, Hourglass, LayoutDashboard, List, Play, Plus, UserCheck, Workflow } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Card } from '../components/ui/Card'
import { EmptyState } from '../components/ui/EmptyState'
import { Skeleton } from '../components/ui/Skeleton'
import { StatCard } from '../components/ui/StatCard'
import { useAuth } from '../features/auth/useAuth'
import * as casosApi from '../features/casos/api'
import { contarPorGrupo } from '../features/casos/situaciones'
import { fechaLarga, primerNombre, saludoSegunHora } from '../lib/saludo'

/** How many cases are in a technical state, for the "needs your attention" row (one row is enough: only the total is read). */
function useTotalEn(estado: string) {
  return useQuery({
    queryKey: ['casos', 'inicio', estado],
    queryFn: () => casosApi.listCasos({ estado, pageSize: 1 }),
    select: (pagina) => pagina.total,
    refetchInterval: 15000,
  })
}

const botonDelHeroe =
  'inline-flex items-center gap-2 rounded-lg px-4 py-2 text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-white'

/**
 * Where you land: who you are and what is going on — what needs a person (failed cases, reviews waiting), what is moving, and your
 * processes one click from their cases. It is a summary, not a second dashboard: every figure leads to the list already filtered.
 */
export function HomePage() {
  const { user, can } = useAuth()
  const nombre = primerNombre(user?.displayName)

  const resumen = useQuery({ queryKey: ['casos-resumen', 'inicio'], queryFn: () => casosApi.getResumen() })
  const fallidos = useTotalEn('Fallido')
  const enRevision = useTotalEn('EsperandoRevisionHumana')

  const procesos = [...(resumen.data ?? [])].sort((a, b) => a.flujoNombre.localeCompare(b.flujoNombre, 'es'))
  const conteoTotal = contarPorGrupo(procesos.flatMap((p) => p.porTipo))

  return (
    <div className="flex flex-col gap-8">
      <section className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-indigo-600 via-indigo-600 to-purple-600 p-6 text-white shadow-card sm:p-8">
        <div aria-hidden="true" className="pointer-events-none absolute -top-16 -right-10 h-56 w-56 rounded-full bg-white/10 blur-2xl" />
        <div aria-hidden="true" className="pointer-events-none absolute -bottom-20 left-1/3 h-48 w-48 rounded-full bg-indigo-300/20 blur-3xl" />
        <p className="relative text-sm font-medium text-indigo-100">{fechaLarga()}</p>
        <h1 className="relative mt-1 text-3xl font-semibold tracking-tight sm:text-4xl">
          {saludoSegunHora()}
          {nombre && `, ${nombre}`}
        </h1>
        <p className="relative mt-2 max-w-xl text-indigo-100">
          {resumen.isLoading
            ? 'Mirando cómo van tus procesos…'
            : procesos.length === 0
              ? 'Todavía no tienes procesos asignados.'
              : `${conteoTotal.ejecutando} en ejecución y ${conteoTotal.pendiente} esperando en cola, en ${procesos.length} ${procesos.length === 1 ? 'proceso' : 'procesos'}.`}
        </p>
        <div className="relative mt-5 flex flex-wrap gap-2.5">
          {can('casos.crear') && (
            <Link to="/casos/nuevo" className={`${botonDelHeroe} bg-white text-indigo-700 hover:bg-indigo-50`}>
              <Plus size={16} aria-hidden="true" />
              Nuevo caso
            </Link>
          )}
          <Link to="/casos" className={`${botonDelHeroe} bg-white/15 text-white hover:bg-white/25`}>
            <LayoutDashboard size={16} aria-hidden="true" />
            Ver el panel
          </Link>
          <Link to="/casos/lista" className={`${botonDelHeroe} bg-white/15 text-white hover:bg-white/25`}>
            <List size={16} aria-hidden="true" />
            Ver los casos
          </Link>
        </div>
      </section>

      <section aria-labelledby="atencion" className="flex flex-col gap-3">
        <h2 id="atencion" className="text-sm font-semibold tracking-wide text-gray-500 uppercase">
          Ahora mismo
        </h2>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <StatCard
            label="Fallidos"
            value={fallidos.data ?? '—'}
            icon={<CircleAlert size={20} />}
            tone="danger"
            hint={fallidos.data === 0 ? 'todo en orden' : 'esperan que los reprocesen'}
            to="/casos/lista?estado=Fallido"
          />
          <StatCard
            label="Esperan revisión"
            value={enRevision.data ?? '—'}
            icon={<UserCheck size={20} />}
            tone="info"
            hint="necesitan a una persona"
            to="/casos/lista?estado=EsperandoRevisionHumana"
          />
          <StatCard
            label="En ejecución"
            value={resumen.isLoading ? '—' : conteoTotal.ejecutando}
            icon={<Play size={20} />}
            tone="brand"
            hint="ahora mismo"
            to="/casos/lista?estado=EnProgreso"
          />
          <StatCard
            label="Pendientes"
            value={resumen.isLoading ? '—' : conteoTotal.pendiente}
            icon={<Hourglass size={20} />}
            tone="warning"
            hint="en la cola"
            to="/casos/lista?estado=Pendiente"
          />
        </div>
      </section>

      <section aria-labelledby="procesos" className="flex flex-col gap-3">
        <h2 id="procesos" className="text-sm font-semibold tracking-wide text-gray-500 uppercase">
          Tus procesos
        </h2>
        {resumen.isLoading ? (
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
            {Array.from({ length: 3 }, (_, i) => (
              <Skeleton key={i} className="h-28 rounded-xl" />
            ))}
          </div>
        ) : procesos.length === 0 ? (
          <Card>
            <EmptyState
              icon={<Workflow size={22} aria-hidden="true" />}
              title="No tienes procesos asignados"
              description="Cuando te asignen uno, aparecerá aquí con sus casos."
            />
          </Card>
        ) : (
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
            {procesos.map((proceso) => {
              const conteo = contarPorGrupo(proceso.porTipo)
              return (
                <Link
                  key={proceso.flujoId}
                  to={`/casos/lista?flujoId=${proceso.flujoId}`}
                  className="group flex flex-col gap-3 rounded-xl border border-gray-200 bg-surface p-4 shadow-card transition-colors hover:border-indigo-300 hover:bg-gray-50"
                >
                  <span className="flex items-start gap-3">
                    <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-indigo-100 text-indigo-600">
                      <Workflow size={20} aria-hidden="true" />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block truncate font-semibold text-gray-900" title={proceso.flujoNombre}>
                        {proceso.flujoNombre}
                      </span>
                      <span className="block text-sm text-gray-500">
                        <span className="num font-mono font-medium text-gray-800">{proceso.total}</span> {proceso.total === 1 ? 'caso' : 'casos'}
                      </span>
                    </span>
                    <ArrowRight size={16} aria-hidden="true" className="mt-1 shrink-0 text-gray-400 transition-transform group-hover:translate-x-0.5 group-hover:text-indigo-600" />
                  </span>
                  <span className="flex flex-wrap gap-1.5 text-xs font-medium">
                    {conteo.ejecutando > 0 && <span className="rounded-full bg-indigo-100 px-2 py-0.5 text-indigo-700">{conteo.ejecutando} en ejecución</span>}
                    {conteo.pendiente > 0 && <span className="rounded-full bg-amber-100 px-2 py-0.5 text-amber-700">{conteo.pendiente} en cola</span>}
                    {conteo.detenido > 0 && <span className="rounded-full bg-gray-200 px-2 py-0.5 text-gray-700">{conteo.detenido} detenidos</span>}
                    <span className="rounded-full bg-green-100 px-2 py-0.5 text-green-700">{conteo.finalizado} finalizados</span>
                  </span>
                </Link>
              )
            })}
          </div>
        )}
      </section>
    </div>
  )
}
