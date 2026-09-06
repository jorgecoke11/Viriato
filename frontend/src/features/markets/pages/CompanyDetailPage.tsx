import { useQuery } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { Card } from '../../../components/ui/Card'
import * as marketsApi from '../api'
import type { CriterionResult } from '../api'
import { PriceSparkline } from '../PriceSparkline'
import { GrahamScoreBadge, SignalBadge } from '../SignalBadge'

const criterionStyles: Record<CriterionResult, string> = {
  Pass: 'text-green-700',
  Fail: 'text-red-700',
  NotApplicable: 'text-gray-400',
}

const criterionIcon: Record<CriterionResult, string> = {
  Pass: '✓',
  Fail: '✗',
  NotApplicable: '–',
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="text-xs text-gray-500">{label}</div>
      <div className="text-lg font-semibold text-gray-900">{value}</div>
    </div>
  )
}

export function CompanyDetailPage() {
  const { ticker } = useParams<{ ticker: string }>()

  const query = useQuery({
    queryKey: ['markets-company', ticker],
    queryFn: () => marketsApi.getCompany(ticker!),
    enabled: Boolean(ticker),
  })

  if (query.isLoading) return <p className="text-gray-500">Cargando…</p>
  if (!query.data) return <p className="text-gray-500">No hay análisis disponible para esta compañía todavía.</p>

  const { analysis, trabajoId } = query.data
  const fmt = (n: number | null, digits = 1) => (n === null ? '—' : n.toFixed(digits))

  return (
    <div className="flex flex-col gap-4">
      <Link to="/mercados" className="text-sm text-gray-500 hover:text-gray-900">
        ← Mercados
      </Link>

      <Card>
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div>
            <h1 className="font-mono text-2xl font-semibold text-gray-900">{analysis.ticker}</h1>
            <p className="text-gray-600">{analysis.name}</p>
            <p className="text-sm text-gray-500">{analysis.sector} · {analysis.industry}</p>
          </div>
          <div className="flex items-center gap-2">
            <GrahamScoreBadge score={analysis.grahamScore} evaluated={analysis.criteriaEvaluated} />
            <SignalBadge signal={analysis.signal} />
          </div>
        </div>

        <div className="mt-4">
          <PriceSparkline prices={analysis.recentPrices} />
        </div>

        <div className="mt-4 grid grid-cols-2 gap-4 sm:grid-cols-4">
          <Metric label="Precio" value={`$${fmt(analysis.priceAtComputation, 2)}`} />
          <Metric label="P/E" value={fmt(analysis.peRatio)} />
          <Metric label="P/B" value={fmt(analysis.pbRatio)} />
          <Metric label="Número de Graham" value={fmt(analysis.grahamNumber, 2)} />
          <Metric label="Valor intrínseco" value={analysis.intrinsicValue !== null ? `$${fmt(analysis.intrinsicValue, 2)}` : '—'} />
          <Metric label="Margen de seguridad" value={analysis.marginOfSafetyPercent !== null ? `${fmt(analysis.marginOfSafetyPercent, 0)}%` : '—'} />
          <Metric label="SMA 50 / 200" value={`${fmt(analysis.sma50, 2)} / ${fmt(analysis.sma200, 2)}`} />
          <Metric label="RSI 14" value={fmt(analysis.rsi14, 0)} />
        </div>

        <p className="mt-4 text-xs text-gray-400">
          Calculado el {new Date(analysis.computedAt).toLocaleString()} —{' '}
          <Link to={`/ops/trabajos/${trabajoId}`} className="hover:underline">ver ejecución</Link>
        </p>
      </Card>

      <Card>
        <h2 className="mb-3 text-sm font-medium text-gray-700">Criterios del inversor defensivo (Graham)</h2>
        <ul className="flex flex-col gap-2">
          {analysis.criteria.map((c) => (
            <li key={c.code} className="flex items-center justify-between border-b border-gray-100 pb-2 text-sm last:border-0">
              <span className="text-gray-700">{c.label}</span>
              <span className={`flex items-center gap-2 font-medium ${criterionStyles[c.result]}`}>
                {c.value && <span className="font-mono text-xs text-gray-400">{c.value}</span>}
                {criterionIcon[c.result]}
              </span>
            </li>
          ))}
        </ul>
      </Card>

      {analysis.fundamentalsHistory.length > 0 && (
        <Card className="overflow-x-auto p-0">
          <h2 className="px-4 pt-4 text-sm font-medium text-gray-700">Fundamentales por año</h2>
          <table className="mt-2 w-full min-w-[640px] text-left text-sm">
            <thead className="border-b border-gray-200 text-gray-500">
              <tr>
                <th className="px-4 py-3 font-medium">Año</th>
                <th className="px-4 py-3 font-medium">EPS</th>
                <th className="px-4 py-3 font-medium">Valor contable/acción</th>
                <th className="px-4 py-3 font-medium">Dividendo/acción</th>
                <th className="px-4 py-3 font-medium">Activo corriente</th>
                <th className="px-4 py-3 font-medium">Pasivo corriente</th>
              </tr>
            </thead>
            <tbody>
              {[...analysis.fundamentalsHistory].reverse().map((y) => (
                <tr key={y.fiscalYear} className="border-b border-gray-100 last:border-0">
                  <td className="px-4 py-3 font-medium text-gray-900">{y.fiscalYear}</td>
                  <td className="px-4 py-3 text-gray-600">{fmt(y.eps, 2)}</td>
                  <td className="px-4 py-3 text-gray-600">{fmt(y.bookValuePerShare, 2)}</td>
                  <td className="px-4 py-3 text-gray-600">{fmt(y.dividendPerShare, 2)}</td>
                  <td className="px-4 py-3 text-gray-600">{fmt(y.currentAssets, 0)}</td>
                  <td className="px-4 py-3 text-gray-600">{fmt(y.currentLiabilities, 0)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}
    </div>
  )
}
