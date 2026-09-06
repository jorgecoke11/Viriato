/**
 * Single source of truth for the app's motion language. Every animated component imports from
 * here (directly or via variants.ts) instead of hard-coding its own duration/easing — that's what
 * keeps the whole app feeling like one coherent system instead of a pile of unrelated animations.
 *
 * Durations and easings match what already existed in index.css's fade-in/scale-in keyframes, so
 * this formalizes the house style rather than inventing a new one.
 */

export const duration = {
  /** Micro-interactions: chevrons, small icon state changes. */
  fast: 0.15,
  /** The default: modal panels, page transitions, accordion expand/collapse, tab switches. */
  base: 0.25,
  /** Larger, more prominent movement. */
  slow: 0.35,
} as const

export const ease = {
  /** Entrances — quick start, gentle settle. Same curve index.css already used for scale-in. */
  out: [0.16, 1, 0.3, 1] as const,
  /** Exits — snappier than entrances. Things should leave faster than they arrive. */
  in: [0.4, 0, 1, 1] as const,
  /** Hover/focus micro-transitions. Same curve index.css's ambient transition already used. */
  standard: [0.4, 0, 0.2, 1] as const,
}

export const spring = {
  /** Accordions, the tab indicator, anything that should feel physically dragged into place. */
  snappy: { type: 'spring', stiffness: 420, damping: 38, mass: 0.9 } as const,
  /** Modal panels, page-level movement — a touch softer than `snappy`. */
  gentle: { type: 'spring', stiffness: 300, damping: 30 } as const,
}
