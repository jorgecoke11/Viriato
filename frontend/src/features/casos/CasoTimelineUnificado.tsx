import { useQuery } from '@tanstack/react-query'
import { Braces, Camera, FolderOpen, History, MessageSquare, Paperclip, Play, Tag, Video, Workflow, type LucideIcon } from 'lucide-react'
import { useMemo, useState, type ReactNode } from 'react'
import { Badge, type BadgeTone } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { EmptyState } from '../../components/ui/EmptyState'
import { FileTypeIcon } from '../../components/ui/FileTypeIcon'
import { FilterChips, type OpcionDeChip } from '../../components/ui/FilterChips'
import { Lightbox, type ElementoDeGaleria } from '../../components/ui/Lightbox'
import { Skeleton } from '../../components/ui/Skeleton'
import { Timeline, TimelineGrupo, TimelineItem, type TonoDeNodo } from '../../components/ui/Timeline'
import * as casosApi from './api'
import type { CasoTimelineItemDto } from './api'
import { AuthenticatedImage, AuthenticatedVideo } from './AuthenticatedMedia'
import { ContenidoDeEvidencia } from './ContenidoDeEvidencia'
import { formatFechaCompleta } from './fechas'
import { formatBytes } from './fileUtils'
import { construirLinea, contarPorTipo, tipoDeEntrada, type EntradaDeLinea, type FiltroDeLinea, type TipoDeEntrada } from './lineaDeTiempo'

// How each kind of entry looks: its icon and colour on the rail, the word on its chip, and the name of its filter.
const estilos: Record<TipoDeEntrada, { icono: LucideIcon; nodo: TonoDeNodo; badge: BadgeTone; etiqueta: string; filtro: string }> = {
  estado: { icono: Tag, nodo: 'indigo', badge: 'brand', etiqueta: 'Estado', filtro: 'Estados' },
  captura: { icono: Camera, nodo: 'purple', badge: 'review', etiqueta: 'Captura', filtro: 'Capturas' },
  video: { icono: Video, nodo: 'blue', badge: 'info', etiqueta: 'Vídeo', filtro: 'Vídeos' },
  archivo: { icono: Paperclip, nodo: 'amber', badge: 'warning', etiqueta: 'Archivo', filtro: 'Archivos' },
  datos: { icono: Braces, nodo: 'green', badge: 'success', etiqueta: 'Datos', filtro: 'Datos' },
  nota: { icono: MessageSquare, nodo: 'gray', badge: 'neutral', etiqueta: 'Nota', filtro: 'Notas' },
}

const ORDEN_DE_FILTROS: TipoDeEntrada[] = ['estado', 'captura', 'video', 'archivo', 'datos', 'nota']

const hora = (iso: string) => new Date(iso).toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })

const MINIATURAS_VISIBLES = 8

function Hora({ iso }: { iso: string }) {
  return (
    <time dateTime={iso} title={formatFechaCompleta(iso)} className="num shrink-0 text-xs text-gray-500">
      {hora(iso)}
    </time>
  )
}

function Paso({ nombre }: { nombre: string | null }) {
  if (!nombre) return null
  return (
    <span className="inline-flex min-w-0 items-center gap-1 text-xs text-gray-500">
      <Workflow size={12} aria-hidden="true" className="shrink-0" />
      <span className="truncate">{nombre}</span>
    </span>
  )
}

/** The frame of an entry: what it is, which step it came from, when, and whatever it has to show. */
function Tarjeta({
  titulo,
  tipo,
  pasoNombre,
  iso,
  hasta,
  children,
}: {
  titulo: string
  tipo: TipoDeEntrada
  pasoNombre: string | null
  iso: string
  /** The end of a span (a gallery): the time is then shown as "de – a". */
  hasta?: string
  children?: ReactNode
}) {
  return (
    <div className="rounded-xl border border-gray-200 bg-surface p-4 shadow-sm transition-shadow hover:shadow-md">
      <div className="flex flex-wrap items-start justify-between gap-x-3 gap-y-1">
        <div className="min-w-0">
          <p className="font-medium break-words text-gray-900">{titulo}</p>
          <div className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1">
            <Badge tone={estilos[tipo].badge} dot={false}>
              {estilos[tipo].etiqueta}
            </Badge>
            <Paso nombre={pasoNombre} />
          </div>
        </div>
        {hasta ? (
          <span className="num shrink-0 text-xs text-gray-500">
            {hora(iso)} – {hora(hasta)}
          </span>
        ) : (
          <Hora iso={iso} />
        )}
      </div>
      {children && <div className="mt-3">{children}</div>}
    </div>
  )
}

function Miniatura({ item, alAbrir, className }: { item: CasoTimelineItemDto; alAbrir: () => void; className: string }) {
  return (
    <button type="button" title={item.titulo} aria-label={`Ampliar: ${item.titulo}`} className={`group relative overflow-hidden rounded-lg border border-gray-200 bg-gray-100 ${className}`} onClick={alAbrir}>
      <AuthenticatedImage
        perezosa
        contenedorClassName="h-full w-full"
        path={casosApi.documentoContenidoPath(item.documentoId!)}
        alt={item.titulo}
        className="h-full w-full object-cover transition-transform duration-200 group-hover:scale-[1.03]"
      />
    </button>
  )
}

function Reproductor({ item }: { item: CasoTimelineItemDto }) {
  const [viendo, setViendo] = useState(false)
  if (viendo) return <AuthenticatedVideo path={casosApi.documentoContenidoPath(item.documentoId!)} className="max-h-80 w-full rounded-lg border border-gray-200" />
  return (
    <button
      type="button"
      className="flex aspect-video w-full max-w-sm items-center justify-center gap-2 rounded-lg border border-gray-200 bg-gray-100 text-sm font-medium text-gray-700 hover:bg-gray-200/70"
      onClick={() => setViendo(true)}
    >
      <Play size={18} aria-hidden="true" />
      Reproducir{item.tamanoBytes !== null ? ` · ${formatBytes(item.tamanoBytes)}` : ''}
    </button>
  )
}

/**
 * The overview of the history of a Caso: what happened, in order, by day — status changes, screenshots (seen right there, a
 * step's run of them as one gallery), extracted data, notes, and the files steps produced. The history only summarises: to
 * download a file you go to Documentos, and every file entry says so and takes you there.
 */
export function CasoTimelineUnificado({ casoId, alVerDocumento }: { casoId: string; alVerDocumento: (documentoId: string) => void }) {
  const query = useQuery({ queryKey: ['caso-timeline', casoId], queryFn: () => casosApi.getTimeline(casoId) })
  const [filtro, setFiltro] = useState<FiltroDeLinea>('todo')
  const [verCaptura, setVerCaptura] = useState<string | null>(null)

  const items = useMemo(() => query.data ?? [], [query.data])
  const cuenta = useMemo(() => contarPorTipo(items), [items])
  const dias = useMemo(() => construirLinea(items, filtro), [items, filtro])

  // Every screenshot of the Caso in the order it was taken: the viewer goes through all of them from wherever it was opened.
  const capturas = useMemo(
    () =>
      items
        .filter((i) => tipoDeEntrada(i) === 'captura')
        .sort((a, b) => new Date(a.occurredAt).getTime() - new Date(b.occurredAt).getTime()),
    [items],
  )
  const elementos: ElementoDeGaleria[] = capturas.map((c) => ({
    id: c.id,
    titulo: c.titulo,
    detalle: [c.pasoNombre, hora(c.occurredAt)].filter(Boolean).join(' · '),
    contenido: <AuthenticatedImage path={casosApi.documentoContenidoPath(c.documentoId!)} alt={c.titulo} className="mx-auto max-h-[70vh] w-auto max-w-full rounded-md" />,
  }))
  const indiceAbierto = verCaptura === null ? null : capturas.findIndex((c) => c.id === verCaptura)

  if (query.isLoading) {
    return (
      <div role="status" aria-label="Cargando el historial" className="flex flex-col gap-3">
        <Skeleton className="h-8 w-2/3" />
        <Skeleton className="h-24 rounded-xl" />
        <Skeleton className="h-24 rounded-xl" />
      </div>
    )
  }

  if (query.isError) {
    return (
      <div className="flex items-center justify-between rounded-xl border border-gray-200 bg-surface p-4">
        <p className="text-sm text-red-600">No se ha podido cargar el historial.</p>
        <Button variant="secondary" size="sm" onClick={() => query.refetch()}>
          Reintentar
        </Button>
      </div>
    )
  }

  if (items.length === 0) {
    return (
      <div className="rounded-xl border border-gray-200 bg-surface">
        <EmptyState icon={<History size={22} />} title="Sin actividad todavía" description="Aquí irán apareciendo los cambios de estado, las capturas y los datos que dejen los pasos del proceso." />
      </div>
    )
  }

  const opciones: OpcionDeChip<FiltroDeLinea>[] = [
    { valor: 'todo', etiqueta: 'Todo', cantidad: cuenta.todo },
    ...ORDEN_DE_FILTROS.filter((t) => cuenta[t] > 0 || filtro === t).map((t) => ({ valor: t, etiqueta: estilos[t].filtro, icono: estilos[t].icono, cantidad: cuenta[t] })),
  ]

  const abrirCaptura = (id: string) => setVerCaptura(id)

  function renderizar(entrada: EntradaDeLinea) {
    if (entrada.clase === 'galeria') {
      const { items: tomas, pasoNombre } = entrada
      const visibles = tomas.slice(0, MINIATURAS_VISIBLES)
      const resto = tomas.length - visibles.length
      return (
        <TimelineItem key={entrada.id} icono={Camera} tono="purple">
          <Tarjeta titulo={`${tomas.length} capturas`} tipo="captura" pasoNombre={pasoNombre} iso={tomas[0].occurredAt} hasta={tomas[tomas.length - 1].occurredAt}>
            <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
              {visibles.map((toma, i) => (
                <span key={toma.id} className="relative block aspect-video">
                  <Miniatura item={toma} alAbrir={() => abrirCaptura(toma.id)} className="h-full w-full" />
                  {resto > 0 && i === visibles.length - 1 && (
                    <button
                      type="button"
                      aria-label={`Ver las ${resto} capturas restantes`}
                      className="absolute inset-0 flex items-center justify-center rounded-lg bg-gray-900/60 text-lg font-semibold text-white hover:bg-gray-900/50"
                      onClick={() => abrirCaptura(toma.id)}
                    >
                      +{resto + 1}
                    </button>
                  )}
                </span>
              ))}
            </div>
          </Tarjeta>
        </TimelineItem>
      )
    }

    const { item, tipo } = entrada
    const { icono, nodo } = estilos[tipo]

    if (tipo === 'estado') {
      return (
        <TimelineItem key={entrada.id} icono={icono} tono={nodo} compacto>
          <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5 py-0.5 text-sm text-gray-700">
            <span>Estado de negocio</span>
            <span aria-hidden="true">→</span>
            <Badge tone="brand" dot={false}>
              {item.titulo}
            </Badge>
            <Hora iso={item.occurredAt} />
          </div>
        </TimelineItem>
      )
    }

    return (
      <TimelineItem key={entrada.id} icono={icono} tono={nodo}>
        <Tarjeta titulo={item.titulo} tipo={tipo} pasoNombre={item.pasoNombre} iso={item.occurredAt}>
          {tipo === 'captura' && (
            <Miniatura item={item} alAbrir={() => abrirCaptura(item.id)} className="block aspect-video w-full max-w-sm" />
          )}
          {tipo === 'video' && <Reproductor item={item} />}
          {tipo === 'archivo' && item.documentoId && item.nombreArchivo && (
            <div className="flex flex-wrap items-center gap-3 rounded-lg bg-gray-50 px-3 py-2.5">
              <FileTypeIcon nombre={item.nombreArchivo} contentType={item.contentType} />
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-gray-900" title={item.nombreArchivo}>
                  {item.nombreArchivo}
                </p>
                <p className="num text-xs text-gray-500">{item.tamanoBytes !== null ? formatBytes(item.tamanoBytes) : item.contentType}</p>
              </div>
              <Button variant="secondary" size="sm" onClick={() => alVerDocumento(item.documentoId!)}>
                <FolderOpen size={14} aria-hidden="true" />
                Ver en Documentos
              </Button>
            </div>
          )}
          {item.contenidoJson && tipo !== 'captura' && tipo !== 'video' && <ContenidoDeEvidencia raw={item.contenidoJson} />}
        </Tarjeta>
      </TimelineItem>
    )
  }

  return (
    <div className="flex flex-col gap-5">
      <FilterChips aria-label="Qué mostrar del historial" opciones={opciones} valor={filtro} alCambiar={setFiltro} />

      <Timeline aria-label="Historial del caso">
        {dias.map((dia) => (
          <TimelineGrupo key={dia.clave} etiqueta={dia.etiqueta} detalle={dia.detalle}>
            {dia.entradas.map(renderizar)}
          </TimelineGrupo>
        ))}
      </Timeline>

      <Lightbox
        elementos={elementos}
        indice={indiceAbierto !== null && indiceAbierto >= 0 ? indiceAbierto : null}
        alCambiar={(i) => setVerCaptura(capturas[i]?.id ?? null)}
        alCerrar={() => setVerCaptura(null)}
      />
    </div>
  )
}
