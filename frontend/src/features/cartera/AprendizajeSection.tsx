import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Brain, CheckCircle2, Scale, Target } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Badge, type BadgeTone } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { ConfirmAction } from '../../components/ui/ConfirmAction'
import { DataTable, type ColumnaDeTabla } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { Skeleton } from '../../components/ui/Skeleton'
import { StatCard } from '../../components/ui/StatCard'
import { Can } from '../../components/Can'
import * as api from './api'
import type { EstadisticaDto, ModeloDto, PropuestaDePesosDto, SenalRecienteDto } from './api'
import { formatPorcentaje } from './formato'

const REGLAS: Record<string, string> = { tendencia: 'Tendencia', rsi: 'RSI', macd: 'MACD', bollinger: 'Bollinger' }
const decimales = new Intl.NumberFormat('es-ES', { maximumFractionDigits: 2 })
const porcentaje = (valor: number) => `${decimales.format(valor)} %`
const fecha = (iso: string) => new Date(iso).toLocaleDateString('es-ES', { dateStyle: 'short' })

const tonoDelModelo: Record<ModeloDto['estado'], BadgeTone> = { Activo: 'success', Anterior: 'neutral', Rechazado: 'warning' }

const columnasDeEstadisticas: ColumnaDeTabla<EstadisticaDto>[] = [
  { clave: 'senal', titulo: 'Señal', celda: (e) => <span className="font-semibold text-gray-900">{e.senal}</span> },
  { clave: 'muestras', titulo: 'Juzgadas', celda: (e) => <span className="num">{e.muestras}</span>, className: 'text-right' },
  { clave: 'acierto', titulo: 'Acertó', celda: (e) => <span className="num">{porcentaje(e.aciertoPorcentaje)}</span>, className: 'text-right' },
  {
    clave: 'retorno',
    titulo: 'Movimiento medio del precio',
    celda: (e) => <span className="num text-gray-600">{formatPorcentaje(e.retornoMedioPorcentaje)}</span>,
    className: 'text-right',
  },
]

const columnasDeModelos: ColumnaDeTabla<ModeloDto>[] = [
  { clave: 'version', titulo: 'Versión', celda: (m) => <span className="num font-semibold text-gray-900">v{m.version}</span> },
  { clave: 'estado', titulo: 'Estado', celda: (m) => <Badge tone={tonoDelModelo[m.estado]}>{m.estado === 'Anterior' ? 'Sustituido' : m.estado}</Badge> },
  { clave: 'fecha', titulo: 'Entrenado', celda: (m) => <span className="num text-gray-600">{fecha(m.creadoEn)}</span> },
  {
    clave: 'precision',
    titulo: 'Acierto con días nuevos',
    celda: (m) => (
      <span className="num" title={`Sin modelo, acertar «lo más común» daría ${porcentaje(m.precisionBase * 100)}`}>
        {porcentaje(m.precision * 100)} <span className="text-xs text-gray-400">(base {porcentaje(m.precisionBase * 100)})</span>
      </span>
    ),
    className: 'text-right',
  },
  { clave: 'motivo', titulo: 'Veredicto', celda: (m) => <span className="text-sm text-gray-600">{m.motivo ?? '—'}</span> },
]

const columnasDeSenales: ColumnaDeTabla<SenalRecienteDto>[] = [
  { clave: 'fecha', titulo: 'Día', celda: (s) => <span className="num text-gray-600">{fecha(s.fecha)}</span> },
  { clave: 'valor', titulo: 'Valor', celda: (s) => <span className="font-semibold text-gray-900">{s.ticker}</span> },
  { clave: 'senal', titulo: 'Señal', celda: (s) => <span>{s.senal}</span> },
  {
    clave: 'ml',
    titulo: 'Modelo',
    celda: (s) => <span className="num text-gray-600">{s.probabilidadMl === null ? '—' : `${porcentaje(s.probabilidadMl * 100)} sube`}</span>,
    className: 'text-right',
  },
  {
    clave: 'resultado',
    titulo: 'Resultado',
    celda: (s) =>
      s.acierto === null || s.retornoPorcentaje === null ? (
        <span className="text-sm text-gray-400">sin juzgar</span>
      ) : (
        <Badge tone={s.acierto ? 'success' : 'danger'}>
          {s.acierto ? 'Acertó' : 'Falló'} · {formatPorcentaje(s.retornoPorcentaje)}
        </Badge>
      ),
  },
  {
    clave: 'caso',
    titulo: 'Caso',
    celda: (s) => (
      <Link to={`/casos/${s.casoId}`} className="text-sm font-medium text-indigo-600 hover:text-indigo-700">
        Ver caso
      </Link>
    ),
  },
]

/** The weights in force against the proposed ones, one rule per line. */
function PropuestaDePesos({ propuesta, actuales, alResolver }: { propuesta: PropuestaDePesosDto; actuales: Record<string, number>; alResolver: () => void }) {
  return (
    <Card className="flex flex-col gap-3">
      <div>
        <h3 className="text-sm font-semibold text-gray-900">Propuesta de nuevos pesos</h3>
        <p className="mt-0.5 text-sm text-gray-600">
          {propuesta.motivo ?? 'Los robots proponen dar más peso a las reglas que han acertado y menos a las que no.'} Basada en {propuesta.muestras} análisis juzgados.
          Hasta que alguien la aplique, los robots siguen con los pesos actuales.
        </p>
      </div>
      <ul className="grid grid-cols-2 gap-x-6 gap-y-1 text-sm sm:grid-cols-4">
        {Object.keys(REGLAS).map((regla) => {
          const antes = actuales[regla] ?? 1
          const despues = propuesta.pesos[regla] ?? antes
          const cambia = Math.abs(despues - antes) > 0.001
          return (
            <li key={regla} className="flex items-baseline justify-between gap-2 border-b border-gray-100 py-1">
              <span className="text-gray-600">{REGLAS[regla]}</span>
              <span className="num">
                {cambia ? (
                  <>
                    <span className="text-gray-400">{decimales.format(antes)} → </span>
                    <span className="font-semibold text-gray-900">{decimales.format(despues)}</span>
                  </>
                ) : (
                  <span className="text-gray-500">{decimales.format(antes)}</span>
                )}
              </span>
            </li>
          )
        })}
      </ul>
      <Can permission="cartera.gestionar">
        <div className="flex flex-wrap gap-2">
          <ConfirmAction
            title="Aplicar los nuevos pesos"
            message="Los robots analizarán con estos pesos a partir del próximo análisis. Los análisis anteriores no cambian."
            confirmLabel="Aplicar"
            pendingLabel="Aplicando…"
            accion={() => api.aplicarPropuesta(propuesta.id)}
            mensajeDeExito="Pesos aplicados."
            alTerminar={alResolver}
            disparador={(abrir) => <Button onClick={abrir}>Aplicar</Button>}
          />
          <ConfirmAction
            title="Descartar la propuesta"
            message="Los robots seguirán con los pesos actuales y volverán a proponer cuando tengan más análisis juzgados."
            confirmLabel="Descartar"
            pendingLabel="Descartando…"
            accion={() => api.descartarPropuesta(propuesta.id)}
            mensajeDeExito="Propuesta descartada."
            alTerminar={alResolver}
            disparador={(abrir) => (
              <Button variant="secondary" onClick={abrir}>
                Descartar
              </Button>
            )}
          />
        </div>
      </Can>
    </Card>
  )
}

/**
 * What the trading robots have learned: their analyses judged against what the price did, the weights of each rule and the retrained model.
 * It says how little there is while there is little — a model only takes the place of the one in force if it beats the obvious guess on days it had not seen.
 */
export function AprendizajeSection() {
  const queryClient = useQueryClient()
  const query = useQuery({ queryKey: ['cartera', 'aprendizaje'], queryFn: api.getAprendizaje, refetchInterval: 60_000 })
  const a = query.data
  const refrescar = () => void queryClient.invalidateQueries({ queryKey: ['cartera', 'aprendizaje'] })

  return (
    <section aria-label="Aprendizaje" className="flex flex-col gap-4">
      <div>
        <h2 className="text-sm font-semibold text-gray-900">Lo que han aprendido los robots</h2>
        <p className="mt-0.5 text-sm text-gray-600">
          Cada análisis se anota y, pasados unos días, se compara con lo que hizo el precio. Con eso proponen ajustar el peso de cada regla y reentrenan un modelo cada
          tarde. Un modelo nuevo solo se usa si acierta más que la apuesta obvia con días que no había visto.
        </p>
      </div>

      {query.isLoading && <Skeleton className="h-24 rounded-xl" role="status" aria-label="Cargando el aprendizaje" />}

      {query.isError && (
        <Card>
          <div className="flex items-center justify-between gap-3">
            <p className="text-sm text-red-600">No se ha podido cargar el aprendizaje.</p>
            <Button variant="secondary" size="sm" onClick={() => query.refetch()}>
              Reintentar
            </Button>
          </div>
        </Card>
      )}

      {a && (
        <>
          <div className="grid grid-cols-2 gap-4 xl:grid-cols-4">
            <StatCard label="Análisis juzgados" value={a.evaluadasTotal} icon={<CheckCircle2 size={20} />} tone="brand" hint={`${a.pendientesDeEvaluar} esperando días`} />
            <StatCard label="Pesos en vigor" value={a.versionDePesos === 0 ? 'Iniciales' : `v${a.versionDePesos}`} icon={<Scale size={20} />} tone="neutral" hint="de las cuatro reglas" />
            <StatCard
              label="Modelo en uso"
              value={a.modeloActivo ? `v${a.modeloActivo.version}` : 'Ninguno'}
              icon={<Brain size={20} />}
              tone={a.modeloActivo ? 'success' : 'neutral'}
              hint={a.modeloActivo ? `${porcentaje(a.modeloActivo.precision * 100)} con días nuevos` : 'ninguno ha superado la prueba'}
            />
            <StatCard
              label="Modelo en la práctica"
              value={a.modeloEnVivo ? porcentaje(a.modeloEnVivo.aciertoPorcentaje) : '—'}
              icon={<Target size={20} />}
              tone="neutral"
              hint={a.modeloEnVivo ? `acierto en ${a.modeloEnVivo.muestras} análisis posteriores` : 'aún sin análisis posteriores'}
            />
          </div>

          {a.propuesta && <PropuestaDePesos propuesta={a.propuesta} actuales={a.pesosActivos} alResolver={refrescar} />}

          <div className="flex flex-col gap-2">
            <h3 className="text-sm font-semibold text-gray-900">Cuánto han acertado las señales</h3>
            <Card className="overflow-hidden p-0">
              {a.estadisticas.length === 0 ? (
                <EmptyState
                  icon={<CheckCircle2 size={22} />}
                  title="Todavía no hay análisis juzgados"
                  description="Un análisis se juzga a los pocos días de hacerse. En cuanto haya los primeros, verás aquí cuánto acertaron."
                />
              ) : (
                <DataTable filas={a.estadisticas} columnas={columnasDeEstadisticas} obtenerId={(e) => e.senal} anchoMinimo="32rem" />
              )}
            </Card>
          </div>

          <div className="flex flex-col gap-2">
            <h3 className="text-sm font-semibold text-gray-900">Modelos entrenados</h3>
            <Card className="overflow-hidden p-0">
              {a.modelos.length === 0 ? (
                <EmptyState icon={<Brain size={22} />} title="Todavía no se ha entrenado ningún modelo" description="El aprendiz reentrena cada tarde, después del cierre del mercado." />
              ) : (
                <DataTable filas={a.modelos} columnas={columnasDeModelos} obtenerId={(m) => m.id} anchoMinimo="48rem" />
              )}
            </Card>
          </div>

          {a.recientes.length > 0 && (
            <div className="flex flex-col gap-2">
              <h3 className="text-sm font-semibold text-gray-900">Últimos análisis</h3>
              <Card className="overflow-hidden p-0">
                <DataTable filas={a.recientes} columnas={columnasDeSenales} obtenerId={(s) => s.casoId} anchoMinimo="44rem" />
              </Card>
            </div>
          )}
        </>
      )}
    </section>
  )
}
