import { motion } from 'framer-motion'
import { useId } from 'react'
import { spring } from '../../lib/motion/tokens'

export function Tabs<T extends string>({
  tabs,
  active,
  onChange,
}: {
  tabs: { value: T; label: string }[]
  active: T
  onChange: (value: T) => void
}) {
  // Scoped per instance so two <Tabs> mounted at once (unlikely today, but cheap to guard against)
  // never fight over the same shared-layout indicator.
  const indicatorId = useId()

  return (
    <div role="tablist" className="flex gap-5 border-b border-gray-200">
      {tabs.map((tab) => {
        const isActive = active === tab.value
        return (
          <button
            key={tab.value}
            type="button"
            role="tab"
            aria-selected={isActive}
            className={`relative -mb-px px-1 pb-2.5 text-sm font-medium transition-colors ${
              isActive ? 'text-indigo-600' : 'text-gray-500 hover:text-gray-700'
            }`}
            onClick={() => onChange(tab.value)}
          >
            {tab.label}
            {isActive && (
              <motion.div
                layoutId={`${indicatorId}-tab-indicator`}
                className="absolute inset-x-0 -bottom-px h-0.5 bg-indigo-600"
                transition={spring.snappy}
              />
            )}
          </button>
        )
      })}
    </div>
  )
}
