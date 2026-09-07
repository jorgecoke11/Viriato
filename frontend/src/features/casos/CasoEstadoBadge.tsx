const styles: Record<string, string> = {
  Iniciado: 'bg-gray-100 text-gray-700',
  EnProgreso: 'bg-blue-100 text-blue-700',
  Pausado: 'bg-amber-100 text-amber-700',
  EsperandoRevisionHumana: 'bg-purple-100 text-purple-700',
  Completado: 'bg-green-100 text-green-700',
  Fallido: 'bg-red-100 text-red-700',
  Cancelado: 'bg-gray-200 text-gray-600',
  Pendiente: 'bg-gray-100 text-gray-700',
  Omitido: 'bg-gray-100 text-gray-400',
  Completada: 'bg-green-100 text-green-700',
  Fallida: 'bg-red-100 text-red-700',
  Cancelada: 'bg-gray-200 text-gray-600',
}

const labels: Record<string, string> = {
  Iniciado: 'Iniciado',
  EnProgreso: 'En progreso',
  Pausado: 'Pausado',
  EsperandoRevisionHumana: 'Esperando revisión',
  Completado: 'Completado',
  Fallido: 'Fallido',
  Cancelado: 'Cancelado',
  Pendiente: 'Pendiente',
  Omitido: 'Omitido',
  Completada: 'Completada',
  Fallida: 'Fallida',
  Cancelada: 'Cancelada',
}

export function CasoEstadoBadge({ estado }: { estado: string }) {
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${styles[estado] ?? 'bg-gray-100 text-gray-700'}`}>
      {labels[estado] ?? estado}
    </span>
  )
}
