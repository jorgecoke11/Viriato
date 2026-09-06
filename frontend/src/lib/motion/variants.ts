import type { Variants } from 'framer-motion'
import { duration, ease, spring } from './tokens'

/** Modal/dialog backdrop. */
export const overlayVariants: Variants = {
  initial: { opacity: 0 },
  animate: { opacity: 1, transition: { duration: duration.fast, ease: ease.standard } },
  exit: { opacity: 0, transition: { duration: duration.fast, ease: ease.in } },
}

/** Modal/dialog panel — settles in with a spring, leaves quickly on a plain tween. */
export const panelVariants: Variants = {
  initial: { opacity: 0, scale: 0.96, y: 4 },
  animate: { opacity: 1, scale: 1, y: 0, transition: spring.gentle },
  exit: { opacity: 0, scale: 0.98, y: 4, transition: { duration: duration.fast, ease: ease.in } },
}

/** Accordion / expand-collapse content. Framer Motion animates `height: 'auto'` directly. */
export const collapseVariants: Variants = {
  initial: { height: 0, opacity: 0 },
  animate: { height: 'auto', opacity: 1, transition: { duration: duration.base, ease: ease.out } },
  exit: { height: 0, opacity: 0, transition: { duration: duration.fast, ease: ease.in } },
}

/** Route-level page transitions — subtle on purpose, not a full-screen slide. */
export const pageVariants: Variants = {
  initial: { opacity: 0, y: 8 },
  animate: { opacity: 1, y: 0, transition: { duration: duration.base, ease: ease.out } },
  exit: { opacity: 0, y: -4, transition: { duration: duration.fast, ease: ease.in } },
}

/** Toasts and the mobile nav panel. */
export const slideInRightVariants: Variants = {
  initial: { opacity: 0, x: 16 },
  animate: { opacity: 1, x: 0, transition: { duration: duration.base, ease: ease.out } },
  exit: { opacity: 0, x: 16, transition: { duration: duration.fast, ease: ease.in } },
}

/** Plain fade — mobile nav backdrop, anything that shouldn't move, just appear/disappear. */
export const fadeVariants: Variants = {
  initial: { opacity: 0 },
  animate: { opacity: 1, transition: { duration: duration.fast, ease: ease.standard } },
  exit: { opacity: 0, transition: { duration: duration.fast, ease: ease.in } },
}
