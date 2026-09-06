import { AnimatePresence, motion } from 'framer-motion'
import { slideInRightVariants } from '../motion/variants'
import type { Toast } from './types'

const styles: Record<Toast['type'], string> = {
  success: 'border-green-200 bg-green-50 text-green-800',
  error: 'border-red-200 bg-red-50 text-red-800',
  info: 'border-gray-200 bg-white text-gray-800',
}

export function ToastContainer({ toasts, onDismiss }: { toasts: Toast[]; onDismiss: (id: string) => void }) {
  // No early-return-when-empty here: the last toast dismissing is exactly the moment `toasts`
  // becomes `[]`, and this container must stay mounted for AnimatePresence to play its exit.
  return (
    <div className="fixed bottom-4 right-4 z-[100] flex flex-col gap-2">
      <AnimatePresence>
        {toasts.map((toast) => (
          <motion.div
            key={toast.id}
            layout
            role="alert"
            variants={slideInRightVariants}
            initial="initial"
            animate="animate"
            exit="exit"
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
          </motion.div>
        ))}
      </AnimatePresence>
    </div>
  )
}
