import { useQuery } from '@tanstack/react-query'
import { Banknote, Coins, TrendingUp, Wallet } from 'lucide-react'
import { Link } from 'react-router-dom'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { DataTable, type ColumnaDeTabla } from '../../components/ui/DataTable'
import { EmptyState } from '../../components/ui/EmptyState'
import { PageHeader } from '../../components/ui/PageHeader'
import { Skeleton } from '../../components/ui/Skeleton'
import { StatCard } from '../../components/ui/StatCard'
import { AprendizajeSection } from './AprendizajeSection'
import * as api from './api'
import type { MovimientoDto, PosicionDto } from './api'
import { formatCantidad, formatConSigno, formatDolares, formatEuros, formatPorcentaje } from './formato'

const fecha = (iso: string) => new Date(iso).toLocaleString('es-ES', { dateStyle: 'short', timeStyle: 'short' })

const columnasDePosiciones: ColumnaDeTabla<PosicionDto>[] = [
  { clave: 'valor', titulo: 'Valor', celda: (p) => <span className="font-semibold text-gray-900">{p.ticker}</span> },
  { clave: 'cantidad', titulo: 'Cantidad', celda: (p) => <span className="num">{formatCantidad(p.cantidad)}</span>, className: 'text-right' },
  { clave: 'coste', titulo: 'Coste medio', celda: (p) => <span className="num">{formatDolares(p.costeMedioUsd)}</span>, className: 'text-right' },
  {
    clave: 'precio',
    titulo: 'Precio actual',
    celda: (p) => (
      <span className="num" title={p.precioEsElCoste ? 'Todavía no hay precio: se valora a su coste.' : p.precioDe ? `Visto ${fecha(p.precioDe)}` : undefined}>
        {formatDolares(p.precioActualUsd)}
        {p.precioEsElCoste && <span className="ml-1 text-xs text-gray-400">(coste)</span>}
      </span>
    ),
    className: 'text-right',
  },
  { clave: 'importe', titulo: 'Valor en €', celda: (p) => <span className="num font-medium">{formatEuros(p.valorEur)}</span>, className: 'text-right' },
  {
    clave: 'ganancia',
    titulo: 'Ganancia',
    celda: (p) => (
      <span className={`num ${p.gananciaEur > 0 ? 'text-green-700' : p.gananciaEur < 0 ? 'text-red-600' : 'text-gray-500'}`}>
        {formatConSigno(p.gananciaEur, formatEuros)} <span className="text-xs">({formatPorcentaje(p.gananciaPorcentaje)})</span>
      </span>
    ),
    className: 'text-right',
  },
  { clave: 'stop', titulo: 'Stop', celda: (p) => <span className="num text-gray-500">{p.stopUsd === null ? '—' : formatDolares(p.stopUsd)}</span>, className: 'text-right' },
]

const columnasDeMovimientos: ColumnaDeTabla<MovimientoDto>[] = [
  { clave: 'fecha', titulo: 'Fecha', celda: (m) => <span className="num text-gray-600">{fecha(m.fecha)}</span> },
  { clave: 'lado', titulo: 'Operación', celda: (m) => <Badge tone={m.lado === 'Compra' ? 'brand' : 'neutral'}>{m.lado}</Badge> },
  { clave: 'valor', titulo: 'Valor', celda: (m) => <span className="font-semibold text-gray-900">{m.ticker}</span> },
  { clave: 'cantidad', titulo: 'Cantidad', celda: (m) => <span className="num">{formatCantidad(m.cantidad)}</span>, className: 'text-right' },
  { clave: 'precio', titulo: 'Precio', celda: (m) => <span className="num">{formatDolares(m.precioUsd)}</span>, className: 'text-right' },
  { clave: 'importe', titulo: 'Importe', celda: (m) => <span className="num font-medium">{formatEuros(m.importeEur)}</span>, className: 'text-right' },
  { clave: 'comision', titulo: 'Comisión', celda: (m) => <span className="num text-gray-500">{formatEuros(m.comisionEur)}</span>, className: 'text-right' },
  {
    clave: 'caso',
    titulo: 'Caso',
    celda: (m) => (
      <Link to={`/casos/${m.casoId}`} className="text-sm font-medium text-indigo-600 hover:text-indigo-700">
        Ver caso
      </Link>
    ),
  },
]

/**
 * The simulated portfolio of the trading robots: what it started with, what it is worth now, what it holds and every order that was executed.
 * It is paper money — nothing here is real and nothing is sent to a broker — and the page says so.
 */
export function CarteraPage() {
  const query = useQuery({ queryKey: ['cartera'], queryFn: api.getCartera, refetchInterval: 30_000 })
  const cartera = query.data

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Cartera simulada"
        description="Dinero de papel: lo que han «comprado» y «vendido» los robots de trading. No hay dinero real ni ningún broker."
        actions={
          <Button variant="secondary" disabled={query.isFetching} onClick={() => query.refetch()}>
            {query.isFetching ? 'Actualizando…' : 'Actualizar'}
          </Button>
        }
      />

      {query.isLoading && (
        <div role="status" aria-label="Cargando la cartera" className="flex flex-col gap-6">
          <div className="grid grid-cols-2 gap-4 xl:grid-cols-4">
            {Array.from({ length: 4 }, (_, i) => (
              <Skeleton key={i} className="h-[84px] rounded-xl" />
            ))}
          </div>
          <Skeleton className="h-48 rounded-xl" />
        </div>
      )}

      {query.isError && (
        <Card>
          <div className="flex items-center justify-between gap-3">
            <p className="text-sm text-red-600">No se ha podido cargar la cartera.</p>
            <Button variant="secondary" size="sm" onClick={() => query.refetch()}>
              Reintentar
            </Button>
          </div>
        </Card>
      )}

      {cartera && (
        <>
          <div className="grid grid-cols-2 gap-4 xl:grid-cols-4">
            <StatCard label="Patrimonio" value={formatEuros(cartera.patrimonioEur)} icon={<Wallet size={20} />} tone="brand" hint={`empezó con ${formatEuros(cartera.capitalInicialEur)}`} />
            <StatCard label="Efectivo" value={formatEuros(cartera.efectivoEur)} icon={<Banknote size={20} />} tone="neutral" hint="sin invertir" />
            <StatCard
              label="Invertido"
              value={formatEuros(cartera.valorPosicionesEur)}
              icon={<Coins size={20} />}
              tone="neutral"
              hint={`${cartera.posiciones.length} ${cartera.posiciones.length === 1 ? 'posición' : 'posiciones'}`}
            />
            <StatCard
              label="Rentabilidad"
              value={formatConSigno(cartera.rentabilidadEur, formatEuros)}
              icon={<TrendingUp size={20} />}
              tone={cartera.rentabilidadEur > 0 ? 'success' : cartera.rentabilidadEur < 0 ? 'danger' : 'neutral'}
              hint={formatPorcentaje(cartera.rentabilidadPorcentaje)}
            />
          </div>

          <section aria-label="Posiciones" className="flex flex-col gap-2">
            <h2 className="text-sm font-semibold text-gray-900">Posiciones abiertas</h2>
            <Card className="overflow-hidden p-0">
              {cartera.posiciones.length === 0 ? (
                <EmptyState icon={<Coins size={22} />} title="No hay posiciones abiertas" description="Cuando una orden aprobada se ejecute, aparecerá aquí lo que se ha comprado." />
              ) : (
                <DataTable filas={cartera.posiciones} columnas={columnasDePosiciones} obtenerId={(p) => p.ticker} anchoMinimo="44rem" />
              )}
            </Card>
          </section>

          <section aria-label="Movimientos" className="flex flex-col gap-2">
            <h2 className="text-sm font-semibold text-gray-900">Movimientos</h2>
            <Card className="overflow-hidden p-0">
              {cartera.movimientos.length === 0 ? (
                <EmptyState icon={<Banknote size={22} />} title="Todavía no hay movimientos" description="Cada compra o venta simulada que ejecute el robot quedará anotada aquí." />
              ) : (
                <DataTable filas={cartera.movimientos} columnas={columnasDeMovimientos} obtenerId={(m) => m.id} anchoMinimo="50rem" />
              )}
            </Card>
          </section>
        </>
      )}

      <AprendizajeSection />
    </div>
  )
}
