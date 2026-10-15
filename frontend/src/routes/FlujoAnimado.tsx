import { motion, useReducedMotion } from 'framer-motion'

const NODOS = [
  { x: 28, etiqueta: 'Caso' },
  { x: 108, etiqueta: 'Robot' },
  { x: 188, etiqueta: 'Revisión' },
  { x: 268, etiqueta: 'Listo' },
]
const Y = 36
const RECORRIDO = 4 // seconds a case takes to cross the whole diagram
const CASOS = 3

/**
 * A flat sketch of what the platform does: cases come in, a robot works them, a person reviews some, they end. Small dots (the cases) travel along
 * the line from one step to the next, one after another; the robot's ring breathes while it works. No glow, no gradient — a picture of the idea.
 * With reduced motion it is the same drawing, still, with the dots spaced along the way.
 */
export function FlujoAnimado() {
  const quieto = useReducedMotion()
  const primero = NODOS[0].x
  const ultimo = NODOS[NODOS.length - 1].x

  return (
    <svg viewBox="0 0 296 84" role="img" aria-label="Esquema: los casos entran, un robot los trabaja, una persona revisa algunos y terminan" className="h-auto w-full max-w-xs">
      <line x1={primero} y1={Y} x2={ultimo} y2={Y} className="stroke-gray-300" strokeWidth={2} strokeDasharray="3 5" strokeLinecap="round" />

      {NODOS.map((nodo, i) => (
        <g key={nodo.etiqueta}>
          {nodo.etiqueta === 'Robot' && !quieto && (
            <motion.circle
              cx={nodo.x}
              cy={Y}
              initial={{ r: 16, opacity: 0.45 }}
              animate={{ r: 26, opacity: 0 }}
              transition={{ duration: 2, repeat: Infinity, ease: 'easeOut' }}
              className="fill-none stroke-indigo-400"
              strokeWidth={2}
            />
          )}
          <circle cx={nodo.x} cy={Y} r={16} className={i === NODOS.length - 1 ? 'fill-green-100 stroke-green-600' : 'fill-surface stroke-indigo-500'} strokeWidth={2} />
          {i === NODOS.length - 1 ? (
            <path d={`M${nodo.x - 6} ${Y} l4.5 4.5 l8 -9`} className="fill-none stroke-green-600" strokeWidth={2.4} strokeLinecap="round" strokeLinejoin="round" />
          ) : (
            <circle cx={nodo.x} cy={Y} r={4} className="fill-indigo-500" />
          )}
          <text x={nodo.x} y={Y + 36} textAnchor="middle" className="fill-gray-500 text-[11px] font-medium">
            {nodo.etiqueta}
          </text>
        </g>
      ))}

      {Array.from({ length: CASOS }, (_, i) =>
        quieto ? (
          <circle key={i} cx={primero + ((ultimo - primero) * (i + 0.5)) / CASOS} cy={Y} r={4.5} className="fill-indigo-600" />
        ) : (
          <motion.circle
            key={i}
            cy={Y}
            r={4.5}
            className="fill-indigo-600"
            initial={{ cx: primero, opacity: 0 }}
            animate={{ cx: [primero, ultimo], opacity: [0, 1, 1, 0] }}
            transition={{
              duration: RECORRIDO,
              repeat: Infinity,
              ease: 'linear',
              delay: (i * RECORRIDO) / CASOS,
              opacity: { times: [0, 0.1, 0.9, 1], duration: RECORRIDO, repeat: Infinity, ease: 'linear', delay: (i * RECORRIDO) / CASOS },
            }}
          />
        ),
      )}
    </svg>
  )
}
