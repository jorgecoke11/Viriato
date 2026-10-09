import { useEffect, useRef, useState } from 'react'
import { Skeleton } from '../../components/ui/Skeleton'
import { apiFetchBlob } from '../../lib/apiClient'

// Documento content is behind Authorization, which <img>/<video src> can't send — this fetches it
// as a Blob once and exposes an Object URL, revoking it on unmount/path change to avoid leaking memory.
// While `activo` is false nothing is fetched: a gallery only pays for the images that come into view.
function useAuthenticatedMediaUrl(path: string, activo = true) {
  const [url, setUrl] = useState<string | null>(null)
  const [error, setError] = useState(false)

  useEffect(() => {
    if (!activo) return
    let objectUrl: string | null = null
    let cancelled = false

    setUrl(null)
    setError(false)

    apiFetchBlob(path)
      .then((blob) => {
        if (cancelled) return
        objectUrl = URL.createObjectURL(blob)
        setUrl(objectUrl)
      })
      .catch(() => {
        if (!cancelled) setError(true)
      })

    return () => {
      cancelled = true
      if (objectUrl) URL.revokeObjectURL(objectUrl)
    }
  }, [path, activo])

  return { url, error }
}

/** True once the element has come near the viewport, and stays true. */
function useCercaDeLaPantalla<T extends Element>() {
  const ref = useRef<T>(null)
  const [cerca, setCerca] = useState(false)

  useEffect(() => {
    const elemento = ref.current
    if (!elemento || cerca) return
    if (typeof IntersectionObserver === 'undefined') {
      setCerca(true)
      return
    }
    const observador = new IntersectionObserver(
      (entradas) => {
        if (entradas.some((e) => e.isIntersecting)) {
          setCerca(true)
          observador.disconnect()
        }
      },
      { rootMargin: '200px' },
    )
    observador.observe(elemento)
    return () => observador.disconnect()
  }, [cerca])

  return { ref, cerca }
}

interface MediaProps {
  path: string
  className?: string
}

interface ImageProps extends MediaProps {
  alt: string
  onClick?: () => void
  /** Fetched only when it comes near the screen, inside a box of `contenedorClassName` that keeps its size meanwhile. */
  perezosa?: boolean
  contenedorClassName?: string
}

export function AuthenticatedImage({ path, alt, className, onClick, perezosa = false, contenedorClassName }: ImageProps) {
  const { ref, cerca } = useCercaDeLaPantalla<HTMLDivElement>()
  const { url, error } = useAuthenticatedMediaUrl(path, !perezosa || cerca)
  // The blob can download fine and still not be a decodable image (corrupt or mislabelled file) —
  // without this the user just sees an empty box and no explanation.
  const [undecodable, setUndecodable] = useState(false)

  useEffect(() => setUndecodable(false), [url])

  let contenido
  if (error) contenido = <p className="p-2 text-xs text-red-600">No se pudo cargar la imagen.</p>
  else if (undecodable) contenido = <p className="p-2 text-xs text-red-600">No es una imagen válida. Puedes descargarla.</p>
  else if (!url) contenido = perezosa ? <Skeleton className="h-full w-full rounded-none" /> : <p className="text-xs text-gray-400">Cargando imagen…</p>
  else {
    contenido = (
      <img
        src={url}
        alt={alt}
        onError={() => setUndecodable(true)}
        onClick={onClick}
        className={className ?? 'max-h-80 rounded-md border border-gray-200'}
      />
    )
  }

  return perezosa ? (
    <div ref={ref} className={contenedorClassName}>
      {contenido}
    </div>
  ) : (
    contenido
  )
}

export function AuthenticatedVideo({ path, className }: MediaProps) {
  const { url, error } = useAuthenticatedMediaUrl(path)

  if (error) return <p className="text-xs text-red-600">No se pudo cargar el vídeo.</p>
  if (!url) return <p className="text-xs text-gray-400">Cargando vídeo…</p>
  return <video src={url} controls className={className ?? 'max-h-80 rounded-md border border-gray-200'} />
}
