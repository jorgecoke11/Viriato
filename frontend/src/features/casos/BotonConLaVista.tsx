import { useQuery } from '@tanstack/react-query'
import { Video } from 'lucide-react'
import { useState } from 'react'
import { IconButton } from '../../components/ui/IconButton'
import * as casosApi from './api'
import { VentanaDeVistaEnDirecto } from './BotonVerEnDirecto'

/**
 * The camera in a list of Casos. A list row only knows that a robot's screen can be watched, not where: the address is looked up
 * when the camera is clicked (one request for one Caso), not for every row on the page.
 */
export function BotonConLaVista({ casoId, titulo }: { casoId: string; titulo: string }) {
  const [pedida, setPedida] = useState(false)
  const [abierta, setAbierta] = useState(false)

  const caso = useQuery({ queryKey: ['caso-vista', casoId], queryFn: () => casosApi.getCaso(casoId), enabled: pedida })
  const url = caso.data?.ejecucionActual?.pasos.find((p) => p.estado === 'EnProgreso' && p.vistaEnDirectoUrl)?.vistaEnDirectoUrl ?? null

  return (
    <>
      <IconButton
        size="sm"
        label={`Ver en directo: ${titulo}`}
        onClick={() => {
          setPedida(true)
          setAbierta(true)
        }}
      >
        <Video size={16} aria-hidden="true" />
        <span aria-hidden="true" className="absolute top-1.5 right-1.5 h-2 w-2 rounded-full bg-red-500 ring-2 ring-surface motion-safe:animate-pulse" />
      </IconButton>
      {abierta && url === null && !caso.isLoading ? (
        // The run ended between the list and the click: say so instead of opening nothing.
        <span role="status" className="text-xs text-gray-500">
          Ya terminó.
        </span>
      ) : (
        url !== null && <VentanaDeVistaEnDirecto abierta={abierta} url={url} titulo={titulo} alCerrar={() => setAbierta(false)} />
      )}
    </>
  )
}
