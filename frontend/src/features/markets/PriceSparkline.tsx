import type { PricePointDto } from './api'

export function PriceSparkline({ prices }: { prices: PricePointDto[] }) {
  if (prices.length < 2) return <p className="text-sm text-gray-500">Sin histórico de precio suficiente.</p>

  const width = 600
  const height = 100
  const closes = prices.map((p) => p.close)
  const min = Math.min(...closes)
  const max = Math.max(...closes)
  const range = max - min || 1

  const points = prices.map((p, i) => {
    const x = (i / (prices.length - 1)) * width
    const y = height - ((p.close - min) / range) * height
    return `${x.toFixed(1)},${y.toFixed(1)}`
  })

  const isUp = closes[closes.length - 1] >= closes[0]

  return (
    <svg viewBox={`0 0 ${width} ${height}`} className="h-24 w-full" preserveAspectRatio="none">
      <polyline
        points={points.join(' ')}
        fill="none"
        stroke={isUp ? '#16a34a' : '#dc2626'}
        strokeWidth={2}
        vectorEffect="non-scaling-stroke"
      />
    </svg>
  )
}
