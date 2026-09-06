import { AnimatePresence, motion } from 'framer-motion'
import { Menu, X } from 'lucide-react'
import { useState } from 'react'
import { Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../features/auth/useAuth'
import { spring } from '../lib/motion/tokens'
import { fadeVariants, pageVariants, slideInRightVariants } from '../lib/motion/variants'
import { SidebarContent } from './sidebar/SidebarContent'
import { getSidebarCollapsed, setSidebarCollapsed } from './sidebar/sidebarPreferences'

const EXPANDED_WIDTH = 256
const COLLAPSED_WIDTH = 72

export function AppLayout() {
  const { status } = useAuth()
  const [mobileNavOpen, setMobileNavOpen] = useState(false)
  const [collapsed, setCollapsed] = useState(getSidebarCollapsed)
  const location = useLocation()

  if (status !== 'authenticated') {
    return (
      <div className="min-h-screen bg-gray-50">
        <Outlet />
      </div>
    )
  }

  const toggleCollapsed = () => {
    setCollapsed((prev) => {
      const next = !prev
      setSidebarCollapsed(next)
      return next
    })
  }

  return (
    <div className="flex min-h-screen bg-gray-50">
      <motion.aside
        className="hidden shrink-0 overflow-hidden border-r border-blue-100 bg-gradient-to-b from-blue-50/40 via-white to-white lg:block"
        animate={{ width: collapsed ? COLLAPSED_WIDTH : EXPANDED_WIDTH }}
        transition={spring.gentle}
      >
        <div className="fixed h-screen" style={{ width: collapsed ? COLLAPSED_WIDTH : EXPANDED_WIDTH }}>
          <SidebarContent collapsed={collapsed} onToggleCollapsed={toggleCollapsed} />
        </div>
      </motion.aside>

      <AnimatePresence>
        {mobileNavOpen && (
          <div className="fixed inset-0 z-40 lg:hidden">
            <motion.div
              className="absolute inset-0 bg-black/30"
              variants={fadeVariants}
              initial="initial"
              animate="animate"
              exit="exit"
              onClick={() => setMobileNavOpen(false)}
            />
            <motion.div
              className="absolute inset-y-0 left-0 w-64 border-r border-blue-100 bg-gradient-to-b from-blue-50/40 via-white to-white shadow-xl"
              variants={slideInRightVariants}
              initial="initial"
              animate="animate"
              exit="exit"
            >
              <div className="flex justify-end px-3 pt-3">
                <button
                  type="button"
                  aria-label="Cerrar menú"
                  className="rounded-md p-1.5 text-gray-400 hover:bg-blue-50 hover:text-blue-700"
                  onClick={() => setMobileNavOpen(false)}
                >
                  <X size={18} />
                </button>
              </div>
              {/* No collapse toggle here — the mobile drawer is a temporary overlay, never an icon rail. */}
              <SidebarContent onNavigate={() => setMobileNavOpen(false)} />
            </motion.div>
          </div>
        )}
      </AnimatePresence>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex items-center gap-3 border-b border-gray-200 bg-white px-4 py-3 lg:hidden">
          <button
            type="button"
            aria-label="Abrir menú"
            className="rounded-md p-1.5 text-gray-500 hover:bg-gray-100"
            onClick={() => setMobileNavOpen(true)}
          >
            <Menu size={20} />
          </button>
          <span className="text-sm font-semibold text-gray-900">Viariato</span>
        </header>

        <main className="flex-1 px-4 py-6 sm:px-6 lg:px-10 lg:py-8">
          <div className="mx-auto max-w-6xl">
            {/* Deliberately no AnimatePresence/exit here: the outgoing page can just unmount
                instantly (React's default) — only the incoming one needs to animate in. Keying by
                pathname gives every route a fresh mount, so motion.div's own initial->animate
                transition runs automatically without needing to orchestrate an exit at all. */}
            <motion.div
              key={location.pathname}
              variants={pageVariants}
              initial="initial"
              animate="animate"
            >
              <Outlet />
            </motion.div>
          </div>
        </main>
      </div>
    </div>
  )
}
