import { useEffect, useState } from 'react'
import { apiFetchBlob } from '../../lib/apiClient'

// Documento content is behind Authorization, which <img>/<video src> can't send — this fetches it
// as a Blob once and exposes an Object URL, revoking it on unmount/path change to avoid leaking memory.
function useAuthenticatedMediaUrl(path: string) {
  const [url, setUrl] = useState<string | null>(null)
  const [error, setError] = useState(false)

  useEffect(() => {
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
  }, [path])

  return { url, error }
}

interface MediaProps {
  path: string
  className?: string
}

export function AuthenticatedImage({ path, alt, className, onClick }: MediaProps & { alt: string; onClick?: () => void }) {
  const { url, error } = useAuthenticatedMediaUrl(path)
  // The blob can download fine and still not be a decodable image (corrupt or mislabelled file) —
  // without this the user just sees an empty box and no explanation.
  const [undecodable, setUndecodable] = useState(false)

  useEffect(() => setUndecodable(false), [url])

  if (error) return <p className="text-xs text-red-600">No se pudo cargar la imagen.</p>
  if (undecodable) return <p className="text-xs text-red-600">El archivo no es una imagen válida, así que no se puede mostrar. Puedes descargarlo.</p>
  if (!url) return <p className="text-xs text-gray-400">Cargando imagen…</p>
  return (
    <img
      src={url}
      alt={alt}
      onError={() => setUndecodable(true)}
      onClick={onClick}
      className={className ?? 'max-h-80 rounded-md border border-gray-200'}
    />
  )
}

export function AuthenticatedVideo({ path, className }: MediaProps) {
  const { url, error } = useAuthenticatedMediaUrl(path)

  if (error) return <p className="text-xs text-red-600">No se pudo cargar el vídeo.</p>
  if (!url) return <p className="text-xs text-gray-400">Cargando vídeo…</p>
  return <video src={url} controls className={className ?? 'max-h-80 rounded-md border border-gray-200'} />
}
