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

export function AuthenticatedImage({ path, alt }: { path: string; alt: string }) {
  const { url, error } = useAuthenticatedMediaUrl(path)

  if (error) return <p className="text-xs text-red-600">No se pudo cargar la imagen.</p>
  if (!url) return <p className="text-xs text-gray-400">Cargando imagen…</p>
  return <img src={url} alt={alt} className="max-h-80 rounded-md border border-gray-200" />
}

export function AuthenticatedVideo({ path }: { path: string }) {
  const { url, error } = useAuthenticatedMediaUrl(path)

  if (error) return <p className="text-xs text-red-600">No se pudo cargar el vídeo.</p>
  if (!url) return <p className="text-xs text-gray-400">Cargando vídeo…</p>
  return <video src={url} controls className="max-h-80 rounded-md border border-gray-200" />
}
