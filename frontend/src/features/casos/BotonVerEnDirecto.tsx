import { ChevronDown, ExternalLink, Video } from 'lucide-react'
import { useState } from 'react'
import { Button } from '../../components/ui/Button'
import { IconButton } from '../../components/ui/IconButton'
import { Modal } from '../../components/ui/Modal'
import { direccionDeVistaSegura, direccionParaElMarco } from './vistaEnDirecto'

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
        // The robot's screen is another site: it gets a frame of its own, with the permissions a viewer needs and no more.
        <iframe
          title={`Ver en directo: ${titulo}`}
          src={direccionParaElMarco(direccion)}
          sandbox="allow-scripts allow-same-origin"
          referrerPolicy="no-referrer"
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
        <iframe
          title={`En directo: ${titulo}`}
          src={direccionParaElMarco(direccion)}
          sandbox="allow-scripts allow-same-origin"
          referrerPolicy="no-referrer"
          className="block aspect-[16/10] max-h-[70vh] w-full border-t border-gray-200 bg-black"
        />
      )}
    </section>
  )
}
