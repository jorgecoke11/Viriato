import { useQuery } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { CheckCheck, Hourglass, Layers, LayoutGrid, Pause, Play, SlidersHorizontal, Workflow } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Button } from '../../../components/ui/Button'
import { Card } from '../../../components/ui/Card'
import { EmptyState } from '../../../components/ui/EmptyState'
import { MultiSelect } from '../../../components/ui/MultiSelect'
import { PageHeader } from '../../../components/ui/PageHeader'
import { Skeleton } from '../../../components/ui/Skeleton'
import { SelectorDeRango } from '../../../components/ui/SelectorDeRango'
import { StatCard } from '../../../components/ui/StatCard'
import { collapseVariants } from '../../../lib/motion/variants'
import { esRangoElegido } from '../../../lib/rangoDeFechas'
import { usePreferencia } from '../../auth/usePreferencia'
import * as casosApi from '../api'
import type { EstadoConteoDto } from '../api'
import { getHiddenFlujoIds } from '../dashboardPreferences'
import { describeFiltro, filtroToParams, FINALIZADOS_DE_HOY, type FinalizadosFiltro } from '../finalizadosFiltro'
import { ProcesoResumenCard, type TipoCasoFiltro } from '../ProcesoResumenCard'
import { contarPorGrupo } from '../situaciones'
import { TipoCasoModal } from '../TipoCasoModal'

const esListaDeTextos = (valor: unknown): valor is string[] => Array.isArray(valor) && valor.every((v) => typeof v === 'string')

export function DashboardPage() {
  // Which finished Casos the panel counts: kept for the person, so it is as they left it.
  const [modoGlobal, setModoGlobal] = usePreferencia<FinalizadosFiltro>('panel:finalizados', FINALIZADOS_DE_HOY, esRangoElegido)
  // The processes the person chose not to see, kept for their user. What an older version saved for the whole browser is where it starts.
  const ocultosHeredados = useMemo(() => [...getHiddenFlujoIds()], [])
  const [ocultos, setOcultos] = usePreferencia<string[]>('panel:procesos-ocultos', ocultosHeredados, esListaDeTextos)
  const hidden = useMemo(() => new Set(ocultos), [ocultos])
  const [managing, setManaging] = useState(false)
  const [modalTipo, setModalTipo] = useState<{
    flujoId: string
    flujoNombre: string
    tipoCasoId: string | null
    tipoCasoNombre: string
    filtro: TipoCasoFiltro
    modo: FinalizadosFiltro
    estados: EstadoConteoDto[]
  } | null>(null)

  const query = useQuery({
    queryKey: ['casos-resumen', modoGlobal],
    queryFn: () => casosApi.getResumen(filtroToParams(modoGlobal)),
  })

  const cajitas = query.data ?? []

  // The picker works with what is shown; what is stored is what is not. Ids of processes no longer assigned are kept as they were.
  const idsDeCajitas = new Set(cajitas.map((c) => c.flujoId))
  const mostrados = new Set(cajitas.filter((c) => !hidden.has(c.flujoId)).map((c) => c.flujoId))
  const cambiarMostrados = (elegidos: Set<string>) =>
    setOcultos([...ocultos.filter((id) => !idsDeCajitas.has(id)), ...cajitas.filter((c) => !elegidos.has(c.flujoId)).map((c) => c.flujoId)])
  const visibles = cajitas.filter((c) => !hidden.has(c.flujoId))

  // The headline figures cover exactly what the cards below show: hide a process and it leaves the totals too.
  const totalCasos = visibles.reduce((suma, c) => suma + c.total, 0)
  const { ejecutando, pendiente: pendientes, detenido: detenidos, finalizado: finalizados } = contarPorGrupo(visibles.flatMap((c) => c.porTipo))

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Panel de casos"
        description={`Los casos en curso siempre se ven; ${describeFiltro(modoGlobal).toLowerCase()}.`}
        actions={
          <>
            <SelectorDeRango
              titulo="Finalizados a mostrar"
              valor={modoGlobal}
              alCambiar={setModoGlobal}
              textoDeTodos="Todos los finalizados"
              resumen={describeFiltro(modoGlobal)}
            />
            <Button variant={managing ? 'primary' : 'secondary'} aria-pressed={managing} onClick={() => setManaging((m) => !m)}>
              <SlidersHorizontal size={16} />
              {managing ? 'Cerrar' : 'Personalizar'}
            </Button>
          </>
        }
      />

      <AnimatePresence initial={false}>
        {managing && (
          <motion.div variants={collapseVariants} initial="initial" animate="animate" exit="exit" className="overflow-hidden">
            <Card>
              <h2 className="mb-1 text-sm font-semibold text-gray-900">Procesos visibles</h2>
              <p className="mb-3 text-sm text-gray-500">Elige qué procesos aparecen en el panel. Se recuerda para tu usuario en este navegador.</p>
              {cajitas.length === 0 ? (
                <p className="text-sm text-gray-500">No tienes flujos asignados todavía.</p>
              ) : (
                <div className="max-w-xl">
                  <MultiSelect
                    etiqueta="Procesos visibles"
                    opciones={cajitas.map((c) => ({ id: c.flujoId, etiqueta: c.flujoNombre }))}
                    seleccion={mostrados}
                    alCambiar={cambiarMostrados}
                    plural="procesos"
                    singular="proceso"
                    ayuda="Se muestran todos. Abre la lista para quitar los que no quieras ver."
                  />
                </div>
              )}
            </Card>
          </motion.div>
        )}
      </AnimatePresence>

      {query.isLoading && (
        <div role="status" aria-label="Cargando el panel" className="flex flex-col gap-6">
          <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            {Array.from({ length: 4 }, (_, i) => (
              <Skeleton key={i} className="h-[84px] rounded-xl" />
            ))}
          </div>
          <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
            {Array.from({ length: 2 }, (_, i) => (
              <Skeleton key={i} className="h-64 rounded-2xl" />
            ))}
          </div>
        </div>
      )}

      {query.isError && (
        <Card>
          <div className="flex items-center justify-between gap-3">
            <p className="text-sm text-red-600">No se ha podido cargar el panel de casos.</p>
            <Button variant="secondary" size="sm" onClick={() => query.refetch()}>
              Reintentar
            </Button>
          </div>
        </Card>
      )}

      {query.isSuccess && cajitas.length === 0 && (
        <Card className="p-0">
          <EmptyState
            icon={<Workflow size={22} />}
            title="Todavía no tienes procesos"
            description="Pide a un administrador que te asigne un flujo para empezar a ver casos aquí."
          />
        </Card>
      )}

      {query.isSuccess && cajitas.length > 0 && (
        <div className={`grid grid-cols-2 gap-4 ${detenidos > 0 ? 'lg:grid-cols-6' : 'lg:grid-cols-5'}`}>
          <StatCard label="Procesos" value={visibles.length} icon={<Workflow size={20} />} tone="brand" hint={`de ${cajitas.length}`} />
          <StatCard label="Casos" value={totalCasos} icon={<Layers size={20} />} tone="neutral" />
          <StatCard
            label="En ejecución"
            value={ejecutando}
            icon={<Play size={20} />}
            tone="brand"
            hint="ahora mismo"
            to="/casos/lista?estado=EnProgreso"
          />
          <StatCard
            label="Pendientes"
            value={pendientes}
            icon={<Hourglass size={20} />}
            tone="warning"
            hint="en la cola"
            to="/casos/lista?estado=Pendiente"
          />
          {detenidos > 0 && (
            <StatCard
              label="Detenidos"
              value={detenidos}
              icon={<Pause size={20} />}
              tone="neutral"
              hint="pausados o en revisión"
              to="/casos/lista?estado=Pausado,EsperandoRevisionHumana,Iniciado"
            />
          )}
          <StatCard label="Finalizados" value={finalizados} icon={<CheckCheck size={20} />} tone="success" hint={describeFiltro(modoGlobal).toLowerCase()} />
        </div>
      )}

      <div className="grid grid-cols-1 items-start gap-4 xl:grid-cols-2">
        {visibles.map((c) => (
          <ProcesoResumenCard
            key={c.flujoId}
            resumen={c}
            modoGlobal={modoGlobal}
            onSelectTipo={(tipo, filtro, modo) =>
              setModalTipo({
                flujoId: c.flujoId,
                flujoNombre: c.flujoNombre,
                tipoCasoId: tipo.tipoCasoId,
                tipoCasoNombre: tipo.nombre,
                filtro,
                modo,
                estados: tipo.porEstado,
              })
            }
          />
        ))}
      </div>

      {cajitas.length > 0 && visibles.length === 0 && (
        <Card className="p-0">
          <EmptyState
            icon={<LayoutGrid size={22} />}
            title="Todos los procesos están ocultos"
            description="Usa «Personalizar» para volver a mostrar alguno."
            action={
              <Button variant="secondary" size="sm" onClick={() => setManaging(true)}>
                Personalizar
              </Button>
            }
          />
        </Card>
      )}

      <TipoCasoModal data={modalTipo} onClose={() => setModalTipo(null)} />

    </div>
  )
}
