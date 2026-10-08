import { Badge, type BadgeTone } from '../../components/ui/Badge'

// How each state of a Caso, its Ejecucion or one of its steps reads: a tone, and the label people see.
const states: Record<string, { tone: BadgeTone; label: string; live?: boolean }> = {
  Iniciado: { tone: 'neutral', label: 'Iniciado' },
  EnProgreso: { tone: 'info', label: 'En progreso', live: true },
  Pausado: { tone: 'warning', label: 'Pausado' },
  EsperandoRevisionHumana: { tone: 'review', label: 'Esperando revisión' },
  Completado: { tone: 'success', label: 'Completado' },
  Fallido: { tone: 'danger', label: 'Fallido' },
  Cancelado: { tone: 'neutral', label: 'Cancelado' },
  Pendiente: { tone: 'neutral', label: 'Pendiente' },
  Omitido: { tone: 'neutral', label: 'Omitido' },
  Completada: { tone: 'success', label: 'Completada' },
  Fallida: { tone: 'danger', label: 'Fallida' },
  Cancelada: { tone: 'neutral', label: 'Cancelada' },
}

export function CasoEstadoBadge({ estado }: { estado: string }) {
  const { tone, label, live } = states[estado] ?? { tone: 'neutral' as const, label: estado }
  return (
    <Badge tone={tone} live={live}>
      {label}
    </Badge>
  )
}
