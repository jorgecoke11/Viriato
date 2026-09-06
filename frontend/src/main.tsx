import { QueryClientProvider } from '@tanstack/react-query'
import { MotionConfig } from 'framer-motion'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router-dom'
import { AuthProvider } from './features/auth/AuthProvider'
import './index.css'
import { duration, ease } from './lib/motion/tokens'
import { queryClient } from './lib/queryClient'
import { ToastProvider } from './lib/toast/ToastProvider'
import { router } from './routes/router'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    {/* Sets the app-wide default transition and honors prefers-reduced-motion in one place —
        every motion.* component inherits this unless it specifies its own. */}
    <MotionConfig reducedMotion="user" transition={{ duration: duration.base, ease: ease.standard }}>
      <QueryClientProvider client={queryClient}>
        <ToastProvider>
          <AuthProvider>
            <RouterProvider router={router} />
          </AuthProvider>
        </ToastProvider>
      </QueryClientProvider>
    </MotionConfig>
  </StrictMode>,
)
