import { useParams } from 'react-router-dom'
import { CasoDetailContent } from '../CasoDetailContent'
import { BackLink } from '../../../components/ui/BackLink'

export function CasoDetailPage() {
  const { id } = useParams<{ id: string }>()

  if (!id) return null

  return (
    <div className="flex flex-col gap-4">
      <BackLink to="/casos/lista">Listado</BackLink>
      <CasoDetailContent casoId={id} />
    </div>
  )
}
