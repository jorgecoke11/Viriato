const styles: Record<string, string> = {
  Bullish: 'bg-green-100 text-green-700',
  Bearish: 'bg-red-100 text-red-700',
  Neutral: 'bg-gray-100 text-gray-700',
}

const labels: Record<string, string> = {
  Bullish: 'Alcista',
  Bearish: 'Bajista',
  Neutral: 'Neutral',
}

export function SignalBadge({ signal }: { signal: string }) {
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${styles[signal] ?? styles.Neutral}`}>
      {labels[signal] ?? signal}
    </span>
  )
}

export function GrahamScoreBadge({ score, evaluated }: { score: number; evaluated: number }) {
  const ratio = evaluated > 0 ? score / evaluated : 0
  const color = ratio >= 0.7 ? 'bg-green-100 text-green-700' : ratio >= 0.4 ? 'bg-amber-100 text-amber-700' : 'bg-gray-100 text-gray-700'
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${color}`}>
      {score}/7
    </span>
  )
}
