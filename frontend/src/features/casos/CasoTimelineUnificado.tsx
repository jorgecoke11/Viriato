import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Modal } from '../../components/ui/Modal'
import * as casosApi from './api'
import type { CasoTimelineItemDto } from './api'
import { AuthenticatedImage, AuthenticatedVideo } from './AuthenticatedMedia'

function EstadoIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className="h-3 w-3">
      <path d="M5 12l4 4L19 6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

function DocumentoIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" className="h-3 w-3">
      <path d="M7 3h7l4 4v14H7z" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M14 3v4h4" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

function ImagenIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" className="h-3 w-3">
      <rect x="3" y="4" width="18" height="16" rx="2" />
      <circle cx="8.5" cy="9.5" r="1.5" />
      <path d="M21 16l-5-5-9 9" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

function VideoIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" className="h-3 w-3">
      <rect x="3" y="5" width="13" height="14" rx="2" />
      <path d="M16 10l5-3v10l-5-3z" strokeLinejoin="round" />
    </svg>
  )
}

const tipoIconStyle: Record<CasoTimelineItemDto['tipo'], string> = {
  EstadoCambiado: 'bg-indigo-100 text-indigo-700',
  Documento: 'bg-gray-100 text-gray-600',
  Evidencia: 'bg-purple-100 text-purple-700',
}

function TimelineIcon({ item }: { item: CasoTimelineItemDto }) {
  if (item.tipo === 'EstadoCambiado') return <EstadoIcon />
  if (item.tipo === 'Documento') return <DocumentoIcon />
  if (item.evidenciaTipo === 'Video') return <VideoIcon />
  return <ImagenIcon />
}

const esMediaVisible = (item: CasoTimelineItemDto) =>
  item.tipo === 'Evidencia' && item.documentoId !== null && (item.evidenciaTipo === 'Screenshot' || item.evidenciaTipo === 'Video')

// Unifies the three things a user actually wants to see in order on a Caso: business-status changes,
// document uploads, and evidence — the technical per-step progress lives one level down, inside each
// Ejecucion (see CasoEjecuciones), never mixed in here.
export function CasoTimelineUnificado({ casoId }: { casoId: string }) {
  const query = useQuery({ queryKey: ['caso-timeline', casoId], queryFn: () => casosApi.getTimeline(casoId) })
  const [media, setMedia] = useState<CasoTimelineItemDto | null>(null)

  if (query.isLoading) return <p className="text-sm text-gray-500">Cargando…</p>

  if (query.isError) {
    return (
      <div className="flex items-center justify-between">
        <p className="text-sm text-red-600">No se ha podido cargar el timeline.</p>
        <button className="text-sm font-medium text-gray-700 hover:text-gray-900" onClick={() => query.refetch()}>
          Reintentar
        </button>
      </div>
    )
  }

  const items = query.data ?? []
  if (items.length === 0) return <p className="text-sm text-gray-500">Sin actividad todavía.</p>

  return (
    <>
      <ol className="flex flex-col gap-0">
        {items.map((item, index) => {
          const clicable = esMediaVisible(item)
          return (
            <li key={item.id} className="relative flex gap-3 pb-4 last:pb-0">
              {index < items.length - 1 && (
                <span className="absolute left-[9px] top-6 h-full w-px bg-gray-200" aria-hidden />
              )}
              <span className={`relative z-10 mt-0.5 flex h-[18px] w-[18px] shrink-0 items-center justify-center rounded-full ${tipoIconStyle[item.tipo]}`}>
                <TimelineIcon item={item} />
              </span>
              <div className="flex flex-1 flex-col text-sm">
                {clicable ? (
                  <button
                    type="button"
                    className="w-fit text-left font-medium text-gray-900 hover:text-indigo-600 hover:underline"
                    onClick={() => setMedia(item)}
                  >
                    {item.titulo}
                  </button>
                ) : (
                  <span className="font-medium text-gray-900">{item.titulo}</span>
                )}
                <span className="text-xs text-gray-400">{new Date(item.occurredAt).toLocaleString()}</span>
                {item.tipo === 'Evidencia' && !clicable && item.contenidoJson && (
                  <pre className="mt-1 max-w-full overflow-x-auto rounded-md bg-gray-50 p-2 text-xs text-gray-700">
                    {formatJson(item.contenidoJson)}
                  </pre>
                )}
              </div>
            </li>
          )
        })}
      </ol>

      <Modal open={Boolean(media?.documentoId)} title={media?.titulo ?? ''} onClose={() => setMedia(null)}>
        {media?.documentoId && media.evidenciaTipo === 'Screenshot' && (
          <AuthenticatedImage path={casosApi.documentoContenidoPath(media.documentoId)} alt={media.titulo} />
        )}
        {media?.documentoId && media.evidenciaTipo === 'Video' && (
          <AuthenticatedVideo path={casosApi.documentoContenidoPath(media.documentoId)} />
        )}
      </Modal>
    </>
  )
}

function formatJson(raw: string): string {
  try {
    return JSON.stringify(JSON.parse(raw), null, 2)
  } catch {
    return raw
  }
}
