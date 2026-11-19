import { Badge, type BadgeTone } from '../../components/ui/Badge'
import type { EstadoDeFamilia } from './api'

const TONOS: Record<EstadoDeFamilia, BadgeTone> = { Abierta: 'info', Cerrada: 'warning', Terminada: 'success' }

export function EstadoDeFamiliaBadge({ estado }: { estado: EstadoDeFamilia }) {
  return (
    <Badge tone={TONOS[estado]} live={estado !== 'Terminada'}>
      {estado === 'Cerrada' ? 'Cerrada' : estado}
    </Badge>
  )
}
