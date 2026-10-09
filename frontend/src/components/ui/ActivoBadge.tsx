import { Badge } from './Badge'

/** "Activo" / "Inactivo" for a record that can be switched off but not deleted. */
export function ActivoBadge({ activo }: { activo: boolean }) {
  return <Badge tone={activo ? 'success' : 'neutral'}>{activo ? 'Activo' : 'Inactivo'}</Badge>
}
