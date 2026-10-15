import { motion } from 'framer-motion'
import type { ReactNode } from 'react'
import { duration, ease } from '../../lib/motion/tokens'

/**
 * Lets what it wraps come in: it fades in while rising a few pixels. Give each sibling a growing `retraso` (in steps of ~0.06 s) and they arrive
 * one after another instead of all at once. With reduced motion only the fade remains (the app's MotionConfig takes the movement away).
 */
export function Aparece({ children, retraso = 0, className }: { children: ReactNode; retraso?: number; className?: string }) {
  return (
    <motion.div
      className={className}
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: duration.slow, ease: ease.out, delay: retraso }}
    >
      {children}
    </motion.div>
  )
}
