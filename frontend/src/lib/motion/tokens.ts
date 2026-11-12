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
  /**
   * Entrances AND exits — moves at once, then settles. Exits use it too, only shorter (`duration.fast`): an ease-in exit starts
   * slowly, exactly when the user is watching, so the interface feels like it hesitates. Leaving fast is what makes exits snappy.
   */
  out: [0.16, 1, 0.3, 1] as const,
  /** Something already on screen moving to another place or shape. */
  inOut: [0.77, 0, 0.175, 1] as const,
  /** Hover/focus micro-transitions. Same curve index.css's ambient transition already used. */
  standard: [0.4, 0, 0.2, 1] as const,
}

/**
 * Springs, the way Apple tunes them: critically damped (no bounce) by default, so things settle without overshooting.
 * `duration` here is Apple's "response" — how quickly it gets there — not a fixed length: a spring settles on its own.
 * Bounce is only for things the user threw (a flick, a drag released with speed), and nothing in Viriato is thrown yet.
 */
export const spring = {
  /** Accordions, the tab indicator, chevrons: quick and exact. */
  snappy: { type: 'spring', bounce: 0, duration: 0.3 } as const,
  /** Modal panels, the side rail: a touch slower, for bigger surfaces. */
  gentle: { type: 'spring', bounce: 0, duration: 0.4 } as const,
}
