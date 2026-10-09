import { motion } from 'framer-motion'
import { useId } from 'react'
import { spring } from '../../lib/motion/tokens'

export function Tabs<T extends string>({
  tabs,
  active,
  onChange,
}: {
  tabs: { value: T; label: string; /** A count beside the label ("Documentos 3"); left out while unknown. */ cantidad?: number }[]
  active: T
  onChange: (value: T) => void
}) {
  // Scoped per instance so two <Tabs> mounted at once (unlikely today, but cheap to guard against)
  // never fight over the same shared-layout indicator.
  const indicatorId = useId()

  return (
    // The outer box scrolls sideways on a narrow screen instead of wrapping or squeezing the labels; the rule under
    // the tabs belongs to the inner one, so the buttons that overlap it by a pixel never overflow the scroller.
    <div className="overflow-x-auto">
      <div role="tablist" className="flex w-max min-w-full gap-1 border-b border-gray-200">
      {tabs.map((tab) => {
        const isActive = active === tab.value
        return (
          <button
            key={tab.value}
            type="button"
            role="tab"
            aria-selected={isActive}
            className={`relative -mb-px shrink-0 rounded-t-md px-3 py-2.5 text-sm font-medium ${
              isActive ? 'text-indigo-600' : 'text-gray-500 hover:bg-gray-100/70 hover:text-gray-800'
            }`}
            onClick={() => onChange(tab.value)}
          >
            {tab.label}
            {tab.cantidad !== undefined && (
              <span className={`num ml-1.5 rounded-full px-1.5 py-0.5 font-mono text-[11px] ${isActive ? 'bg-indigo-100 text-indigo-700' : 'bg-gray-100 text-gray-500'}`}>{tab.cantidad}</span>
            )}
            {isActive && (
              <motion.div
                layoutId={`${indicatorId}-tab-indicator`}
                className="absolute inset-x-2 -bottom-px h-0.5 rounded-full bg-indigo-600"
                transition={spring.snappy}
              />
            )}
          </button>
        )
      })}
      </div>
    </div>
  )
}
