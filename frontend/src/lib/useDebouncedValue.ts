import { useEffect, useState } from 'react'

/** The value, but only once it has stopped changing for `retardo` ms — so a search box does not ask the server per keystroke. */
export function useDebouncedValue<T>(valor: T, retardo = 300): T {
  const [retrasado, setRetrasado] = useState(valor)

  useEffect(() => {
    const temporizador = setTimeout(() => setRetrasado(valor), retardo)
    return () => clearTimeout(temporizador)
  }, [valor, retardo])

  return retrasado
}
