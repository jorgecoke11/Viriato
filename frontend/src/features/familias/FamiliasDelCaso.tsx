import { useQuery } from '@tanstack/react-query'
import { Layers } from 'lucide-react'
import { Link } from 'react-router-dom'
import * as api from './api'

/** The families a case belongs to, as links under its title. Nothing when it belongs to none. */
export function FamiliasDelCaso({ casoId }: { casoId: string }) {
  const { data } = useQuery({ queryKey: ['familia', 'del-caso', casoId], queryFn: () => api.familiasDelCaso(casoId) })
  if (!data || data.length === 0) return null

  return (
    <p className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-gray-500">
      <Layers size={14} aria-hidden="true" />
      {data.map((f) => (
        <Link key={f.id} to={`/familias/${f.id}`} className="font-medium text-indigo-600 hover:text-indigo-700">
          {f.nombre}
          <span className="ml-1 text-xs font-normal text-gray-500">({api.describirProgreso(f)})</span>
        </Link>
      ))}
    </p>
  )
}
