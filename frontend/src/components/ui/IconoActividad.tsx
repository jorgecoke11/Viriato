import { motion, useReducedMotion } from 'framer-motion'

const BARRAS = [
  { x: 4, alturas: [6, 14, 8], retraso: 0 },
  { x: 10, alturas: [14, 6, 14], retraso: 0.2 },
  { x: 16, alturas: [8, 14, 6], retraso: 0.4 },
]

/** Three bars that rise and fall: something is working right now. Still (and a little lower) with reduced motion. */
export function IconoActividad({ size = 20 }: { size?: number }) {
  const quieto = useReducedMotion()
  return (
    <svg width={size} height={size} viewBox="0 0 22 22" aria-hidden="true" className="fill-current">
      {BARRAS.map(({ x, alturas, retraso }) => (
        <motion.rect
          key={x}
          x={x}
          width={3}
          rx={1.5}
          initial={false}
          animate={quieto ? { y: 8, height: 10 } : { y: alturas.map((a) => 18 - a), height: alturas }}
          transition={quieto ? { duration: 0 } : { duration: 1.1, repeat: Infinity, ease: 'easeInOut', delay: retraso }}
        />
      ))}
    </svg>
  )
}
