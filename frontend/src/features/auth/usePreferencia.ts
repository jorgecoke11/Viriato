import { useCallback, useEffect, useState } from 'react'
import { claveDePreferencia, guardarPreferencia, leerPreferencia } from '../../lib/preferencias'
import { useAuth } from './useAuth'

/**
 * `useState` for a choice the person should find as they left it: what they picked is kept (in this browser, under their own
 * user) and comes back next time. `valida` guards against a saved value the page no longer understands.
 */
export function usePreferencia<T>(nombre: string, inicial: T, valida: (valor: unknown) => valor is T): [T, (valor: T) => void] {
  const usuarioId = useAuth().user?.id ?? null
  const clave = claveDePreferencia(usuarioId, nombre)
  const [valor, setValor] = useState<T>(() => leerPreferencia(clave, inicial, valida))

  // Another person signs in on the same page, or the preference is another one (a different card): read theirs.
  useEffect(() => {
    setValor(leerPreferencia(clave, inicial, valida))
    // `inicial` and `valida` are expected to be stable for a given preference; only the key decides what is read.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clave])

  const cambiar = useCallback(
    (nuevo: T) => {
      setValor(nuevo)
      guardarPreferencia(clave, nuevo)
    },
    [clave],
  )

  return [valor, cambiar]
}
