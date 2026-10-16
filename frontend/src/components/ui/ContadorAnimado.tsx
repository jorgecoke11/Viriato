import { animate, useReducedMotion } from 'framer-motion'
import { useEffect, useRef, useState } from 'react'
import { ease } from '../../lib/motion/tokens'

/**
 * A whole number that counts up (or down) to its value instead of appearing: it starts from 0 the first time and from what it showed
 * before when the value changes. With reduced motion it is just the number.
 */
export function ContadorAnimado({ valor }: { valor: number }) {
  const reducir = useReducedMotion()
  const [mostrado, setMostrado] = useState(0)
  const desde = useRef(0)

  useEffect(() => {
    if (reducir) return
    const control = animate(desde.current, valor, {
      duration: 0.8,
      ease: ease.out,
      onUpdate: (v) => {
        desde.current = v
        setMostrado(Math.round(v))
      },
    })
    return () => control.stop()
  }, [valor, reducir])

  return <>{reducir ? valor : mostrado}</>
}
