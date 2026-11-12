import type { Variants } from 'framer-motion'
import { duration, ease, spring } from './tokens'

/** Modal/dialog backdrop. */
export const overlayVariants: Variants = {
  initial: { opacity: 0 },
  animate: { opacity: 1, transition: { duration: duration.fast, ease: ease.standard } },
  exit: { opacity: 0, transition: { duration: duration.fast, ease: ease.out } },
}

/** Modal/dialog panel — settles in with a spring, leaves quickly on a plain tween. */
export const panelVariants: Variants = {
  initial: { opacity: 0, scale: 0.96, y: 4 },
  animate: { opacity: 1, scale: 1, y: 0, transition: spring.gentle },
  exit: { opacity: 0, scale: 0.98, y: 4, transition: { duration: duration.fast, ease: ease.out } },
}

/** Accordion / expand-collapse content. Framer Motion animates `height: 'auto'` directly. */
export const collapseVariants: Variants = {
  initial: { height: 0, opacity: 0 },
  animate: { height: 'auto', opacity: 1, transition: { duration: duration.base, ease: ease.out } },
  exit: { height: 0, opacity: 0, transition: { duration: duration.fast, ease: ease.out } },
}

/**
 * Route-level page transitions. Navigating is done dozens of times a day, so it only fades, and quickly: movement on something
 * that frequent reads as the app being slow, not as polish.
 */
export const pageVariants: Variants = {
  initial: { opacity: 0 },
  animate: { opacity: 1, transition: { duration: duration.fast, ease: ease.out } },
  exit: { opacity: 0, transition: { duration: duration.fast, ease: ease.out } },
}

/** The content of a tab, swapped in place (with `AnimatePresence mode="wait"`): a short rise in, a plain fade out. */
export const tabPanelVariants: Variants = {
  initial: { opacity: 0, y: 4 },
  animate: { opacity: 1, y: 0, transition: { duration: duration.fast, ease: ease.out } },
  exit: { opacity: 0, transition: { duration: duration.fast, ease: ease.out } },
}

/** Toasts and the mobile nav panel. */
export const slideInRightVariants: Variants = {
  initial: { opacity: 0, x: 16 },
  animate: { opacity: 1, x: 0, transition: { duration: duration.base, ease: ease.out } },
  exit: { opacity: 0, x: 16, transition: { duration: duration.fast, ease: ease.out } },
}

/** Plain fade — mobile nav backdrop, anything that shouldn't move, just appear/disappear. */
export const fadeVariants: Variants = {
  initial: { opacity: 0 },
  animate: { opacity: 1, transition: { duration: duration.fast, ease: ease.standard } },
  exit: { opacity: 0, transition: { duration: duration.fast, ease: ease.out } },
}
