import { useQuery } from '@tanstack/react-query'
import { ChevronDown, ExternalLink, Video } from 'lucide-react'
import { useId, useState } from 'react'
import { Button } from '../../components/ui/Button'
import { IconButton } from '../../components/ui/IconButton'
import { Modal } from '../../components/ui/Modal'
import * as casosApi from './api'
import { direccionDeVistaSegura, direccionParaElMarco, necesitaPaseDeViriato } from './vistaEnDirecto'

/**
 * The frame with the robot's screen. When the screen is served by Viriato's own address, the frame first gets a pass (a cookie the
 * server only sends to /vista) and waits for it; each frame asks for its own, so it always opens with a full hour left. A screen
 * anywhere else loads straight away.
 */
function MarcoDeVista({ direccion, titulo, className }: { direccion: string; titulo: string; className: string }) {
  const conPase = necesitaPaseDeViriato(direccion)
  const marco = useId()
  const pase = useQuery({
    queryKey: ['pase-de-vista', marco],
    queryFn: async () => {
      await casosApi.pedirPaseDeVista()
      return true
    },
    enabled: conPase,
    staleTime: Infinity,
    gcTime: 0,
    refetchOnWindowFocus: false,
  })

  if (conPase && pase.isError) {
    return (
      <div role="alert" className={`flex items-center justify-center p-6 text-sm text-gray-500 ${className}`}>
        No se pudo abrir la pantalla del robot: {pase.error.message}
      </div>
    )
  }
  if (conPase && !pase.isSuccess) {
    return (
      <div role="status" className={`flex items-center justify-center text-sm text-gray-400 motion-safe:animate-pulse ${className}`}>
        Conectando con la pantalla del robot…
      </div>
    )
  }

  return (
    // The robot's screen is another program: it gets a frame of its own, with the permissions a viewer needs and no more.
    <iframe
      title={titulo}
      src={direccionParaElMarco(direccion)}
      sandbox="allow-scripts allow-same-origin"
      referrerPolicy="no-referrer"
      className={className}
    />
  )
}

/** The window with the robot's screen, live, over the page (and a way to open it in its own tab). Nothing here controls the robot. */
export function VentanaDeVistaEnDirecto({
  abierta,
  url,
  titulo,
  alCerrar,
}: {
  abierta: boolean
  url: string
  titulo: string
  alCerrar: () => void
}) {
  const direccion = direccionDeVistaSegura(url)
  if (direccion === null) return null

  return (
    <Modal
      open={abierta}
      size="xl"
      title={
        <span className="flex items-center gap-2">
          <span aria-hidden="true" className="h-2.5 w-2.5 rounded-full bg-red-500 motion-safe:animate-pulse" />
          En directo — {titulo}
        </span>
      }
      onClose={alCerrar}
      footer={
        <div className="flex items-center justify-between gap-3">
          <span className="text-xs text-gray-500">Solo se mira: no se puede tocar el robot desde aquí.</span>
          <a
            href={direccion}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1.5 text-sm font-medium text-indigo-600 hover:text-indigo-700"
          >
            <ExternalLink size={14} aria-hidden="true" />
            Abrir en otra pestaña
          </a>
        </div>
      }
    >
      {abierta && (
        <MarcoDeVista
          direccion={direccion}
          titulo={`Ver en directo: ${titulo}`}
          className="h-[60vh] w-full rounded-lg border border-gray-200 bg-black"
        />
      )}
    </Modal>
  )
}

/**
 * The camera of a Caso a robot is running: a click opens the robot's screen, live. It is only drawn when the robot said where its
 * screen is and the address is a web one.
 */
export function BotonVerEnDirecto({ url, titulo, variante = 'icono' }: { url: string | null | undefined; titulo: string; variante?: 'icono' | 'boton' }) {
  const [abierto, setAbierto] = useState(false)
  const direccion = direccionDeVistaSegura(url)
  if (direccion === null) return null

  return (
    <>
      {variante === 'icono' ? (
        <IconButton size="sm" label={`Ver en directo: ${titulo}`} onClick={() => setAbierto(true)}>
          <Video size={16} aria-hidden="true" />
          <span aria-hidden="true" className="absolute top-1.5 right-1.5 h-2 w-2 rounded-full bg-red-500 ring-2 ring-surface motion-safe:animate-pulse" />
        </IconButton>
      ) : (
        <Button variant="secondary" onClick={() => setAbierto(true)}>
          <span className="relative flex">
            <Video size={15} aria-hidden="true" />
            <span aria-hidden="true" className="absolute -top-1 -right-1 h-2 w-2 rounded-full bg-red-500 motion-safe:animate-pulse" />
          </span>
          Ver en directo
        </Button>
      )}
      <VentanaDeVistaEnDirecto abierta={abierto} url={direccion} titulo={titulo} alCerrar={() => setAbierto(false)} />
    </>
  )
}

/**
 * The robot's screen inside the Caso itself, as long as it runs: the page shows what the robot is doing, live, the way a Selenium
 * Hub would, without leaving Viriato. It can be folded away, and opened in a tab of its own. Nothing here controls the robot.
 */
export function PanelEnDirecto({ url, titulo }: { url: string | null | undefined; titulo: string }) {
  const [visible, setVisible] = useState(true)
  const direccion = direccionDeVistaSegura(url)
  if (direccion === null) return null

  return (
    <section aria-label="Ejecución en directo" className="overflow-hidden rounded-2xl border border-gray-200 bg-surface shadow-card">
      <div className="flex flex-wrap items-center justify-between gap-3 px-5 py-3">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-900">
          <span aria-hidden="true" className="h-2.5 w-2.5 rounded-full bg-red-500 motion-safe:animate-pulse" />
          En directo
          <span className="font-normal text-gray-500">· solo se mira, no se puede tocar el robot</span>
        </h2>
        <div className="flex items-center gap-2">
          <a
            href={direccion}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1.5 text-sm font-medium text-indigo-600 hover:text-indigo-700"
          >
            <ExternalLink size={14} aria-hidden="true" />
            Abrir en otra pestaña
          </a>
          <Button variant="secondary" size="sm" aria-expanded={visible} onClick={() => setVisible((v) => !v)}>
            <ChevronDown size={14} aria-hidden="true" className={visible ? 'rotate-180 transition-transform' : 'transition-transform'} />
            {visible ? 'Ocultar' : 'Mostrar'}
          </Button>
        </div>
      </div>
      {visible && (
        <MarcoDeVista
          direccion={direccion}
          titulo={`En directo: ${titulo}`}
          className="block aspect-[16/10] max-h-[70vh] w-full border-t border-gray-200 bg-black"
        />
      )}
    </section>
  )
}
