import type { Toast } from './types'

const styles: Record<Toast['type'], string> = {
  success: 'border-green-200 bg-green-50 text-green-800',
  error: 'border-red-200 bg-red-50 text-red-800',
  info: 'border-gray-200 bg-white text-gray-800',
}

export function ToastContainer({ toasts, onDismiss }: { toasts: Toast[]; onDismiss: (id: string) => void }) {
  if (toasts.length === 0) return null

  return (
    <div className="fixed bottom-4 right-4 z-[100] flex flex-col gap-2">
      {toasts.map((toast) => (
        <div
          key={toast.id}
          role="alert"
          className={`flex items-start gap-3 rounded-md border px-4 py-3 text-sm shadow-md ${styles[toast.type]}`}
        >
          <span className="flex-1">{toast.message}</span>
          <button
            type="button"
            className="text-current opacity-60 hover:opacity-100"
            onClick={() => onDismiss(toast.id)}
            aria-label="Cerrar"
          >
            ×
          </button>
        </div>
      ))}
    </div>
  )
}
