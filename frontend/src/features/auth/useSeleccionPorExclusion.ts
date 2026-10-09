import { useCallback, useMemo } from 'react'
import { elegidosSegunExcluidos, excluidosTrasElegir } from '../../lib/opciones'
import { usePreferencia } from './usePreferencia'

const esListaDeTextos = (valor: unknown): valor is string[] => Array.isArray(valor) && valor.every((v) => typeof v === 'string')

/**
 * What the person has chosen among `ids`, kept for their user as what they left OUT (see `elegidosSegunExcluidos`): everything
 * starts chosen. It is the same idea as the dashboard's "Personalizar", for any screen with a picker (`MultiSelect`).
 * `forzada` is a choice that comes from elsewhere (a link that says "only this process"): it wins until the person changes it.
 */
export function useSeleccionPorExclusion(nombre: string, ids: readonly string[], forzada?: ReadonlySet<string> | null) {
  const [excluidos, setExcluidos] = usePreferencia<string[]>(nombre, [], esListaDeTextos)
  const clave = ids.join('|')

  const elegidos = useMemo(
    () => forzada ?? elegidosSegunExcluidos(ids, excluidos),
    // `ids` is read through its key: a new array with the same ids is not a change.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [forzada, clave, excluidos],
  )

  const cambiar = useCallback(
    (nuevos: Set<string>) => setExcluidos(excluidosTrasElegir(ids, nuevos, forzada ? [] : excluidos)),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [clave, excluidos, forzada, setExcluidos],
  )

  return { elegidos, cambiar }
}
