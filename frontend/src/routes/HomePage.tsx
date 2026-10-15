import { useQuery } from '@tanstack/react-query'
import { ArrowRight, CircleAlert, Hourglass, LayoutDashboard, List, Play, UserCheck, Workflow } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Card } from '../components/ui/Card'
import { Aparece } from '../components/ui/Aparece'
import { EmptyState } from '../components/ui/EmptyState'
import { IconoActividad } from '../components/ui/IconoActividad'
import { Skeleton } from '../components/ui/Skeleton'
import { StatCard } from '../components/ui/StatCard'
import { useAuth } from '../features/auth/useAuth'
import * as casosApi from '../features/casos/api'
import { contarPorGrupo } from '../features/casos/situaciones'
import { fechaLarga, primerNombre, saludoSegunHora } from '../lib/saludo'
import { FlujoAnimado } from './FlujoAnimado'

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
  'inline-flex items-center gap-2 rounded-lg px-4 py-2 text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500'

/**
 * Where you land: who you are and what is going on — what needs a person (failed cases, reviews waiting), what is moving, and your
 * processes one click from their cases. It is a summary, not a second dashboard: every figure leads to the list already filtered.
 */
export function HomePage() {
  const { user } = useAuth()
  const nombre = primerNombre(user?.displayName)

  const resumen = useQuery({ queryKey: ['casos-resumen', 'inicio'], queryFn: () => casosApi.getResumen() })
  const fallidos = useTotalEn('Fallido')
  const enRevision = useTotalEn('EsperandoRevisionHumana')

  const procesos = [...(resumen.data ?? [])].sort((a, b) => a.flujoNombre.localeCompare(b.flujoNombre, 'es'))
  const conteoTotal = contarPorGrupo(procesos.flatMap((p) => p.porTipo))

  return (
    <div className="flex flex-col gap-8">
      <Aparece>
        <section className="grid items-center gap-8 rounded-xl border border-gray-200 bg-surface p-6 shadow-card sm:p-8 md:grid-cols-[minmax(0,1fr)_auto]">
          <div>
            <p className="text-sm font-medium text-gray-500">{fechaLarga()}</p>
            <h1 className="mt-1 text-3xl font-semibold tracking-tight text-gray-900 sm:text-4xl">
              {saludoSegunHora()}
              {nombre && `, ${nombre}`}
            </h1>
            <p className="mt-2 max-w-xl text-gray-600">
              {resumen.isLoading
                ? 'Mirando cómo van tus procesos…'
                : procesos.length === 0
                  ? 'Todavía no tienes procesos asignados.'
                  : `${conteoTotal.ejecutando} en ejecución y ${conteoTotal.pendiente} esperando en cola, en ${procesos.length} ${procesos.length === 1 ? 'proceso' : 'procesos'}.`}
            </p>
            <div className="mt-5 flex flex-wrap gap-2.5">
              <Link to="/casos" className={`${botonDelHeroe} border border-gray-300 bg-surface text-gray-700 hover:bg-gray-50`}>
                <LayoutDashboard size={16} aria-hidden="true" />
                Ver el panel
              </Link>
              <Link to="/casos/lista" className={`${botonDelHeroe} border border-gray-300 bg-surface text-gray-700 hover:bg-gray-50`}>
                <List size={16} aria-hidden="true" />
                Ver los casos
              </Link>
            </div>
          </div>
          <div className="hidden md:block">
            <FlujoAnimado />
          </div>
        </section>
      </Aparece>

      <section aria-labelledby="atencion" className="flex flex-col gap-3">
        <h2 id="atencion" className="text-sm font-semibold tracking-wide text-gray-500 uppercase">
          Ahora mismo
        </h2>
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <Aparece retraso={0.10}>
          <StatCard
            label="Fallidos"
            value={fallidos.data ?? '—'}
            icon={<CircleAlert size={20} />}
            tone="danger"
            hint={fallidos.data === 0 ? 'todo en orden' : 'esperan que los reprocesen'}
            to="/casos/lista?estado=Fallido"
          />
          </Aparece>
          <Aparece retraso={0.17}>
          <StatCard
            label="Esperan revisión"
            value={enRevision.data ?? '—'}
            icon={<UserCheck size={20} />}
            tone="info"
            hint="necesitan a una persona"
            to="/casos/lista?estado=EsperandoRevisionHumana"
          />
          </Aparece>
          <Aparece retraso={0.24}>
          <StatCard
            label="En ejecución"
            value={resumen.isLoading ? '—' : conteoTotal.ejecutando}
            icon={conteoTotal.ejecutando > 0 ? <IconoActividad /> : <Play size={20} />}
            tone="brand"
            hint="ahora mismo"
            to="/casos/lista?estado=EnProgreso"
          />
          </Aparece>
          <Aparece retraso={0.31}>
          <StatCard
            label="Pendientes"
            value={resumen.isLoading ? '—' : conteoTotal.pendiente}
            icon={<Hourglass size={20} />}
            tone="warning"
            hint="en la cola"
            to="/casos/lista?estado=Pendiente"
          />
          </Aparece>
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
            {procesos.map((proceso, indice) => {
              const conteo = contarPorGrupo(proceso.porTipo)
              return (
                <Aparece key={proceso.flujoId} retraso={0.35 + Math.min(indice, 6) * 0.06}>
                <Link
                  to={`/casos/lista?flujoId=${proceso.flujoId}`}
                  className="group flex flex-col gap-3 rounded-xl border border-gray-200 bg-surface p-4 shadow-card transition-[border-color,background-color,transform,box-shadow] hover:-translate-y-0.5 hover:border-indigo-300 hover:bg-gray-50 hover:shadow-md"
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
                        <span className="num font-medium text-gray-800">{proceso.total}</span> {proceso.total === 1 ? 'caso' : 'casos'}
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
                </Aparece>
              )
            })}
          </div>
        )}
      </section>
    </div>
  )
}
