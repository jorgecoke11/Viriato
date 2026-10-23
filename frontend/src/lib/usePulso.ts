import { useQuery, useQueryClient, type QueryKey } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'

export interface Pulso {
  firma: string
}

/**
 * Keeps a screen alive without reloading it: asks the server for a short signature every `cadaMs` and, only when it changes,
 * invalidates the queries the screen shows (`refrescar`), so they come back from the server. Nothing is asked while the tab is
 * hidden, and coming back to it asks at once. The first signature only sets the baseline: what is on screen was just loaded.
 */
export function usePulso({
  clave,
  leer,
  cadaMs,
  refrescar,
  habilitado = true,
}: {
  clave: QueryKey
  leer: () => Promise<Pulso>
  cadaMs: number | false
  refrescar: QueryKey[]
  habilitado?: boolean
}) {
  const queryClient = useQueryClient()
  const pulso = useQuery({
    queryKey: ['pulso', ...clave],
    queryFn: leer,
    enabled: habilitado,
    refetchInterval: cadaMs,
    refetchOnWindowFocus: true,
    staleTime: 0,
  })

  // The last signature seen, with the screen it belongs to: another case is another baseline, not a change.
  const huella = JSON.stringify(clave)
  const anterior = useRef<{ huella: string; firma: string } | null>(null)
  const firma = pulso.data?.firma
  useEffect(() => {
    if (firma === undefined) return
    const cambio = anterior.current !== null && anterior.current.huella === huella && anterior.current.firma !== firma
    anterior.current = { huella, firma }
    if (cambio) for (const queryKey of refrescar) void queryClient.invalidateQueries({ queryKey })
    // `refrescar` is a fresh array each render; what matters is the signature.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [firma, huella, queryClient])
}
