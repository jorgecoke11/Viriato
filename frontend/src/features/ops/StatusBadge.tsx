const styles: Record<string, string> = {
  Pending: 'bg-gray-100 text-gray-700',
  Running: 'bg-blue-100 text-blue-700',
  Completed: 'bg-green-100 text-green-700',
  Failed: 'bg-red-100 text-red-700',
}

const labels: Record<string, string> = {
  Pending: 'Pendiente',
  Running: 'En curso',
  Completed: 'Completado',
  Failed: 'Fallido',
}

export function StatusBadge({ status }: { status: string }) {
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${styles[status] ?? 'bg-gray-100 text-gray-700'}`}>
      {labels[status] ?? status}
    </span>
  )
}
