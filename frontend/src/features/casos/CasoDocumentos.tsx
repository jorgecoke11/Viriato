import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Bot, Download, Tags, Upload, User } from 'lucide-react'
import { useEffect, useRef, useState, type ChangeEvent } from 'react'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { EmptyState } from '../../components/ui/EmptyState'
import { FileTypeIcon } from '../../components/ui/FileTypeIcon'
import { Skeleton } from '../../components/ui/Skeleton'
import { ApiError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import * as casosApi from './api'
import type { DocumentoDto, TipoDocumentoDto } from './api'
import { formatFecha, formatFechaCompleta } from './fechas'
import { descargarDocumento, formatBytes } from './fileUtils'

/** The files of the Caso: the ones people upload and the ones its steps generate (reports, exports), each downloadable. */
export function CasoDocumentos({ casoId, resaltarId }: { casoId: string; resaltarId?: string | null }) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()

  const documentosQuery = useQuery({ queryKey: ['caso-documentos', casoId], queryFn: () => casosApi.listDocumentos(casoId) })
  const tiposQuery = useQuery({ queryKey: ['tipos-documento-all'], queryFn: () => casosApi.listTiposDocumento() })

  const uploadMutation = useMutation({
    mutationFn: (file: File) => casosApi.uploadDocumento(casoId, file),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['caso-documentos', casoId] })
      showToast('success', 'Documento subido.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo subir el documento.'),
  })

  function handleFileChange(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (file) uploadMutation.mutate(file)
  }

  const documentos = documentosQuery.data ?? []
  const tipos = tiposQuery.data?.items.filter((t) => t.activo) ?? []

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p className="text-sm text-gray-500">Los archivos que se suben al caso y los que generan sus pasos.</p>
        <label className="inline-flex cursor-pointer items-center gap-2 rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:brightness-110 aria-disabled:pointer-events-none aria-disabled:opacity-50">
          <Upload size={15} aria-hidden="true" />
          {uploadMutation.isPending ? 'Subiendo…' : 'Subir documento'}
          <input type="file" className="hidden" onChange={handleFileChange} disabled={uploadMutation.isPending} />
        </label>
      </div>

      {documentosQuery.isLoading && (
        <div role="status" aria-label="Cargando los documentos" className="flex flex-col gap-3">
          <Skeleton className="h-20 rounded-xl" />
          <Skeleton className="h-20 rounded-xl" />
        </div>
      )}
      {documentosQuery.isError && <p className="text-sm text-red-600">No se han podido cargar los documentos.</p>}
      {documentosQuery.isSuccess && documentos.length === 0 && (
        <Card className="p-0">
          <EmptyState icon={<Upload size={22} />} title="Sin documentos" description="Sube un archivo al caso, o aparecerán aquí los que generen sus pasos." />
        </Card>
      )}

      <div className="flex flex-col gap-3">
        {documentos.map((documento) => (
          <DocumentoCard key={documento.id} documento={documento} tipos={tipos} casoId={casoId} resaltado={documento.id === resaltarId} />
        ))}
      </div>
    </div>
  )
}

function DocumentoCard({
  documento,
  tipos,
  casoId,
  resaltado,
}: {
  documento: DocumentoDto
  tipos: TipoDocumentoDto[]
  casoId: string
  resaltado: boolean
}) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [tipoId, setTipoId] = useState('')
  const [desde, setDesde] = useState('')
  const [hasta, setHasta] = useState('')
  const [downloading, setDownloading] = useState(false)
  const [clasificando, setClasificando] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  // Arriving from the history ("Ver en Documentos"): bring this one into view.
  useEffect(() => {
    if (resaltado) ref.current?.scrollIntoView({ behavior: 'smooth', block: 'center' })
  }, [resaltado])

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['caso-documentos', casoId] })

  const addMutation = useMutation({
    mutationFn: () => casosApi.createClasificacion(documento.id, { tipoDocumentoId: tipoId, paginaDesde: Number(desde), paginaHasta: Number(hasta) }),
    onSuccess: () => {
      invalidate()
      setTipoId('')
      setDesde('')
      setHasta('')
      showToast('success', 'Clasificación añadida.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo añadir la clasificación.'),
  })

  const removeMutation = useMutation({
    mutationFn: (id: string) => casosApi.deleteClasificacion(documento.id, id),
    onSuccess: () => {
      invalidate()
      showToast('success', 'Clasificación eliminada.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo eliminar la clasificación.'),
  })

  async function handleDownload() {
    setDownloading(true)
    try {
      await descargarDocumento(documento.id, documento.nombre)
    } catch {
      showToast('error', 'No se pudo descargar el documento.')
    } finally {
      setDownloading(false)
    }
  }

  const generado = documento.ejecucionPasoId !== null

  return (
    <div ref={ref}>
      <Card className={`flex flex-col gap-3 p-4 ${resaltado ? 'ring-2 ring-indigo-500' : ''}`}>
        <div className="flex flex-wrap items-center gap-3">
          <FileTypeIcon nombre={documento.nombre} contentType={documento.contentType} className="h-11 w-11" />
          <div className="min-w-0 flex-1">
            <p className="truncate font-medium text-gray-900" title={documento.nombre}>
              {documento.nombre}
            </p>
            <div className="mt-0.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-gray-500">
              <span className="num">{formatBytes(documento.tamanoBytes)}</span>
              <span aria-hidden="true">·</span>
              <span className="num" title={formatFechaCompleta(documento.createdAt)}>
                {formatFecha(documento.createdAt)}
              </span>
              <Badge tone={generado ? 'info' : 'neutral'} dot={false} className="gap-1">
                {generado ? <Bot size={11} aria-hidden="true" /> : <User size={11} aria-hidden="true" />}
                {generado ? (documento.pasoNombre ? `Paso «${documento.pasoNombre}»` : 'Generado por un paso') : 'Subido a mano'}
              </Badge>
            </div>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            {tipos.length > 0 && (
              <Button variant="ghost" size="sm" aria-expanded={clasificando} onClick={() => setClasificando((c) => !c)}>
                <Tags size={14} aria-hidden="true" />
                Clasificar
              </Button>
            )}
            <Button variant="secondary" size="sm" disabled={downloading} onClick={handleDownload}>
              <Download size={14} aria-hidden="true" />
              {downloading ? 'Descargando…' : 'Descargar'}
            </Button>
          </div>
        </div>

        {documento.clasificaciones.length > 0 && (
          <div className="flex flex-wrap gap-1.5">
            {documento.clasificaciones.map((clasificacion) => (
              <span key={clasificacion.id} className="inline-flex items-center gap-1 rounded-full bg-gray-100 px-2.5 py-1 text-xs text-gray-700">
                {clasificacion.tipoDocumentoNombre} · p. {clasificacion.paginaDesde}–{clasificacion.paginaHasta}
                <button
                  type="button"
                  aria-label={`Quitar la clasificación ${clasificacion.tipoDocumentoNombre}`}
                  className="text-gray-400 hover:text-red-600"
                  disabled={removeMutation.isPending}
                  onClick={() => removeMutation.mutate(clasificacion.id)}
                >
                  ×
                </button>
              </span>
            ))}
          </div>
        )}

        {clasificando && tipos.length > 0 && (
          <div className="flex flex-wrap items-end gap-2 border-t border-gray-100 pt-3">
            <div className="flex flex-col gap-1">
              <label className="text-xs text-gray-500">Tipo</label>
              <select className="field min-h-9 px-2 py-1.5 text-sm" value={tipoId} onChange={(e) => setTipoId(e.target.value)}>
                <option value="">Seleccionar…</option>
                {tipos.map((tipo) => (
                  <option key={tipo.id} value={tipo.id}>
                    {tipo.nombre}
                  </option>
                ))}
              </select>
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-xs text-gray-500">Desde</label>
              <input type="number" min={1} className="field min-h-9 w-20 px-2 py-1.5 text-sm" value={desde} onChange={(e) => setDesde(e.target.value)} />
            </div>
            <div className="flex flex-col gap-1">
              <label className="text-xs text-gray-500">Hasta</label>
              <input type="number" min={1} className="field min-h-9 w-20 px-2 py-1.5 text-sm" value={hasta} onChange={(e) => setHasta(e.target.value)} />
            </div>
            <Button variant="secondary" size="sm" disabled={!tipoId || !desde || !hasta || addMutation.isPending} onClick={() => addMutation.mutate()}>
              Añadir clasificación
            </Button>
          </div>
        )}
      </Card>
    </div>
  )
}
