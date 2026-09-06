import { Link, useParams } from 'react-router-dom'
import { CasoDetailContent } from '../CasoDetailContent'

export function CasoDetailPage() {
  const { id } = useParams<{ id: string }>()

  if (!id) return null

  return (
    <div className="flex flex-col gap-4">
      <Link to="/casos/lista" className="text-sm text-gray-500 hover:text-gray-900">← Listado</Link>
      <CasoDetailContent casoId={id} />
    </div>
  )
}
