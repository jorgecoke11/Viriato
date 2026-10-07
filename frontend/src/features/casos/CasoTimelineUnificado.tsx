import { useQuery } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { useState, type ReactNode } from 'react'
import { Modal } from '../../components/ui/Modal'
import { collapseVariants } from '../../lib/motion/variants'
import { useToast } from '../../lib/toast/useToast'
import * as casosApi from './api'
import type { CasoTimelineItemDto } from './api'
import { AuthenticatedImage, AuthenticatedVideo } from './AuthenticatedMedia'
import { descargarDocumento, formatBytes } from './fileUtils'

type Kind = 'estado' | 'documento' | 'captura' | 'video' | 'archivo' | 'datos' | 'nota'

function kindOf(item: CasoTimelineItemDto): Kind {
  if (item.tipo === 'EstadoCambiado') return 'estado'
  if (item.tipo === 'Documento') return 'documento'
  switch (item.evidenciaTipo) {
    case 'Screenshot':
      return 'captura'
    case 'Video':
      return 'video'
    case 'ArchivoGenerado':
      return 'archivo'
    case 'DatosExtraidos':
      return 'datos'
    default:
      return 'nota'
  }
}

const kindStyle: Record<Kind, { label: string; node: string; badge: string }> = {
  estado: { label: 'Estado', node: 'bg-indigo-100 text-indigo-700', badge: 'bg-indigo-50 text-indigo-700' },
  documento: { label: 'Documento', node: 'bg-sky-100 text-sky-700', badge: 'bg-sky-50 text-sky-700' },
  captura: { label: 'Captura', node: 'bg-purple-100 text-purple-700', badge: 'bg-purple-50 text-purple-700' },
  video: { label: 'Vídeo', node: 'bg-pink-100 text-pink-700', badge: 'bg-pink-50 text-pink-700' },
  archivo: { label: 'Archivo', node: 'bg-amber-100 text-amber-700', badge: 'bg-amber-50 text-amber-700' },
  datos: { label: 'Datos', node: 'bg-emerald-100 text-emerald-700', badge: 'bg-emerald-50 text-emerald-700' },
  nota: { label: 'Nota', node: 'bg-gray-100 text-gray-600', badge: 'bg-gray-100 text-gray-600' },
}

function Icon({ children }: { children: ReactNode }) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" className="h-4 w-4" strokeLinecap="round" strokeLinejoin="round">
      {children}
    </svg>
  )
}

const kindIcon: Record<Kind, ReactNode> = {
  estado: <Icon><path d="M5 12l4 4L19 6" /></Icon>,
  documento: (
    <Icon>
      <path d="M7 3h7l4 4v14H7z" />
      <path d="M14 3v4h4" />
    </Icon>
  ),
  captura: (
    <Icon>
      <rect x="3" y="4" width="18" height="16" rx="2" />
      <circle cx="8.5" cy="9.5" r="1.5" />
      <path d="M21 16l-5-5-9 9" />
    </Icon>
  ),
  video: (
    <Icon>
      <rect x="3" y="5" width="13" height="14" rx="2" />
      <path d="M16 10l5-3v10l-5-3z" />
    </Icon>
  ),
  archivo: (
    <Icon>
      <path d="M12 3v12" />
      <path d="M7 11l5 5 5-5" />
      <path d="M5 21h14" />
    </Icon>
  ),
  datos: (
    <Icon>
      <path d="M8 6l-5 6 5 6" />
      <path d="M16 6l5 6-5 6" />
    </Icon>
  ),
  nota: (
    <Icon>
      <path d="M4 5h16v11H9l-5 4z" />
    </Icon>
  ),
}

function Chevron({ open }: { open: boolean }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      className={`h-4 w-4 shrink-0 text-gray-400 transition-transform ${open ? 'rotate-180' : ''}`}
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="M6 9l6 6 6-6" />
    </svg>
  )
}

const esImagen = (item: CasoTimelineItemDto) =>
  item.documentoId !== null && (item.contentType?.startsWith('image/') === true || item.evidenciaTipo === 'Screenshot')

const esVideo = (item: CasoTimelineItemDto) =>
  item.documentoId !== null && (item.contentType?.startsWith('video/') === true || item.evidenciaTipo === 'Video')

// An evidencia's contenidoJson is free-form: a plain message, or an object/array worth pretty-printing.
function parseContenido(raw: string): { kind: 'text' | 'json'; text: string } {
  try {
    const value: unknown = JSON.parse(raw)
    if (typeof value === 'string') return { kind: 'text', text: value }
    return { kind: 'json', text: JSON.stringify(value, null, 2) }
  } catch {
    return { kind: 'text', text: raw }
  }
}

function CopyButton({ text }: { text: string }) {
  const [copied, setCopied] = useState(false)

  async function copy() {
    try {
      await navigator.clipboard.writeText(text)
      setCopied(true)
      window.setTimeout(() => setCopied(false), 1500)
    } catch {
      // Clipboard can be blocked (insecure context, permissions) — nothing useful to tell the user.
    }
  }

  return (
    <button type="button" onClick={copy} className="text-xs font-medium text-gray-500 hover:text-gray-900">
      {copied ? 'Copiado' : 'Copiar'}
    </button>
  )
}

function ArchivoInfo({ item }: { item: CasoTimelineItemDto }) {
  const { showToast } = useToast()
  const [downloading, setDownloading] = useState(false)

  if (!item.documentoId || !item.nombreArchivo) return null
  const documentoId = item.documentoId
  const nombre = item.nombreArchivo

  async function handleDownload() {
    setDownloading(true)
    try {
      await descargarDocumento(documentoId, nombre)
    } catch {
      showToast('error', 'No se pudo descargar el archivo.')
    } finally {
      setDownloading(false)
    }
  }

  return (
    <div className="flex items-center justify-between gap-3 rounded-lg bg-gray-50 px-3 py-2">
      <div className="min-w-0">
        <div className="truncate text-sm font-medium text-gray-800">{nombre}</div>
        <div className="text-xs text-gray-500">
          {[item.contentType, item.tamanoBytes !== null ? formatBytes(item.tamanoBytes) : null].filter(Boolean).join(' · ')}
        </div>
      </div>
      <button
        type="button"
        disabled={downloading}
        onClick={handleDownload}
        className="shrink-0 rounded-md border border-gray-300 bg-white px-3 py-1 text-xs font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-60"
      >
        {downloading ? 'Descargando…' : 'Descargar'}
      </button>
    </div>
  )
}

function TimelineDetalle({ item, onAmpliar }: { item: CasoTimelineItemDto; onAmpliar: () => void }) {
  const contenido = item.contenidoJson ? parseContenido(item.contenidoJson) : null
  const hayArchivo = item.documentoId !== null
  const sinDetalle = item.tipo !== 'EstadoCambiado' && !hayArchivo && !contenido

  return (
    <div className="flex flex-col gap-3 border-t border-gray-100 px-4 py-3 text-sm">
      {item.pasoNombre && (
        <div className="text-xs text-gray-500">
          Paso: <span className="font-medium text-gray-700">{item.pasoNombre}</span>
        </div>
      )}

      {item.tipo === 'EstadoCambiado' && (
        <p className="text-gray-700">
          El estado de negocio del caso pasó a <span className="font-medium text-gray-900">{item.titulo}</span>
          {item.estadoCodigo && (
            <>
              {' '}
              <code className="rounded bg-gray-100 px-1.5 py-0.5 text-xs text-gray-600">{item.estadoCodigo}</code>
            </>
          )}
          .
        </p>
      )}

      {item.documentoId && esImagen(item) && (
        <AuthenticatedImage
          path={casosApi.documentoContenidoPath(item.documentoId)}
          alt={item.titulo}
          onClick={onAmpliar}
          className="max-h-72 w-fit max-w-full cursor-zoom-in rounded-lg border border-gray-200 object-contain"
        />
      )}
      {item.documentoId && esVideo(item) && (
        <AuthenticatedVideo path={casosApi.documentoContenidoPath(item.documentoId)} className="max-h-80 w-full rounded-lg border border-gray-200" />
      )}

      <ArchivoInfo item={item} />

      {contenido?.kind === 'text' && <p className="whitespace-pre-wrap text-gray-700">{contenido.text}</p>}
      {contenido?.kind === 'json' && (
        <div className="rounded-lg border border-gray-200">
          <div className="flex items-center justify-between border-b border-gray-200 bg-gray-50 px-3 py-1.5">
            <span className="text-xs font-medium text-gray-500">JSON</span>
            <CopyButton text={contenido.text} />
          </div>
          <pre className="max-h-72 overflow-auto p-3 text-xs text-gray-800">{contenido.text}</pre>
        </div>
      )}

      {sinDetalle && <p className="text-gray-400">Sin más detalle.</p>}
    </div>
  )
}

// Unifies the three things a user actually wants to see in order on a Caso: business-status changes,
// document uploads, and evidence — the technical per-step progress lives one level down, inside each
// Ejecucion (see CasoEjecuciones), never mixed in here. Each entry expands to show whatever is
// relevant for it (message, JSON, screenshot, video, file); media is only fetched once opened.
export function CasoTimelineUnificado({ casoId }: { casoId: string }) {
  const query = useQuery({ queryKey: ['caso-timeline', casoId], queryFn: () => casosApi.getTimeline(casoId) })
  const [abiertos, setAbiertos] = useState<Set<string>>(new Set())
  const [ampliada, setAmpliada] = useState<CasoTimelineItemDto | null>(null)

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

  function toggle(id: string) {
    setAbiertos((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  return (
    <>
      <ol className="relative flex flex-col gap-3">
        <span className="absolute bottom-4 left-[13px] top-4 w-px bg-gray-200" aria-hidden />
        {items.map((item) => {
          const kind = kindOf(item)
          const style = kindStyle[kind]
          const abierto = abiertos.has(item.id)
          return (
            <li key={item.id} className="relative pl-11">
              <span className={`absolute left-0 top-3 z-10 flex h-7 w-7 items-center justify-center rounded-full ring-4 ring-white ${style.node}`}>
                {kindIcon[kind]}
              </span>
              <div className="overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm transition-shadow hover:shadow-md">
                <button
                  type="button"
                  aria-expanded={abierto}
                  onClick={() => toggle(item.id)}
                  className="flex w-full items-center gap-3 px-4 py-3 text-left"
                >
                  <div className="flex min-w-0 flex-1 flex-col gap-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-medium text-gray-900">{item.titulo}</span>
                      <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${style.badge}`}>{style.label}</span>
                    </div>
                    <span className="text-xs text-gray-400">{new Date(item.occurredAt).toLocaleString()}</span>
                  </div>
                  <Chevron open={abierto} />
                </button>
                <AnimatePresence initial={false}>
                  {abierto && (
                    <motion.div variants={collapseVariants} initial="initial" animate="animate" exit="exit" className="overflow-hidden">
                      <TimelineDetalle item={item} onAmpliar={() => setAmpliada(item)} />
                    </motion.div>
                  )}
                </AnimatePresence>
              </div>
            </li>
          )
        })}
      </ol>

      <Modal open={ampliada !== null} title={ampliada?.titulo ?? ''} onClose={() => setAmpliada(null)} size="lg">
        {ampliada?.documentoId && (
          <AuthenticatedImage
            path={casosApi.documentoContenidoPath(ampliada.documentoId)}
            alt={ampliada.titulo}
            className="mx-auto max-h-[70vh] w-auto max-w-full rounded-md"
          />
        )}
      </Modal>
    </>
  )
}
