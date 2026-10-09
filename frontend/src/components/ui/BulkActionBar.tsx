import { AnimatePresence, motion } from 'framer-motion'
import { X } from 'lucide-react'
import type { ReactNode } from 'react'
import { Button } from './Button'

/**
 * The bar that appears at the bottom of a list once something is selected: how many, the actions that can be taken on
 * all of them (as children), and a way to start over. It stays in view while the list scrolls under it.
 */
export function BulkActionBar({
  cantidad,
  singular,
  plural,
  alLimpiar,
  children,
}: {
  cantidad: number
  /** What the selected things are called: "caso" / "casos". */
  singular: string
  plural: string
  alLimpiar: () => void
  children: ReactNode
}) {
  return (
    <AnimatePresence>
      {cantidad > 0 && (
        <motion.div
          role="region"
          aria-label="Acciones sobre la selección"
          className="sticky bottom-4 z-20 mx-auto flex w-full max-w-3xl flex-wrap items-center gap-x-4 gap-y-2 rounded-xl border border-gray-200 bg-raised px-4 py-3 shadow-pop"
          initial={{ opacity: 0, y: 16 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0, y: 16 }}
          transition={{ duration: 0.15 }}
        >
          <p className="text-sm text-gray-700" aria-live="polite">
            <span className="num font-semibold text-gray-900">{cantidad}</span> {cantidad === 1 ? singular : plural}{' '}
            {cantidad === 1 ? 'seleccionado' : 'seleccionados'}
          </p>
          <div className="ml-auto flex flex-wrap items-center gap-2">
            <Button variant="ghost" size="sm" onClick={alLimpiar}>
              <X size={14} aria-hidden="true" />
              Quitar selección
            </Button>
            {children}
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  )
}
