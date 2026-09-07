import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type ChangeEvent } from 'react'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { ApiError, apiFetchBlob } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import * as casosApi from './api'
import type { DocumentoDto, TipoDocumentoDto } from './api'

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  const units = ['KB', 'MB', 'GB']
  let value = bytes / 1024
  let unitIndex = 0
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024
    unitIndex += 1
  }
  return `${value.toFixed(1)} ${units[unitIndex]}`
}

export function CasoDocumentos({ casoId }: { casoId: string }) {
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
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-medium text-gray-900">Documentos</h3>
        <label className="inline-flex cursor-pointer items-center rounded-lg bg-indigo-600 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-indigo-500 aria-disabled:pointer-events-none aria-disabled:opacity-50">
          {uploadMutation.isPending ? 'Subiendo…' : 'Subir documento'}
          <input type="file" className="hidden" onChange={handleFileChange} disabled={uploadMutation.isPending} />
        </label>
      </div>

      {documentosQuery.isLoading && <p className="text-sm text-gray-500">Cargando…</p>}
      {documentosQuery.isError && <p className="text-sm text-red-600">No se han podido cargar los documentos.</p>}
      {documentosQuery.isSuccess && documentos.length === 0 && <p className="text-sm text-gray-500">Sin documentos.</p>}

      <div className="flex flex-col gap-3">
        {documentos.map((documento) => (
          <DocumentoCard key={documento.id} documento={documento} tipos={tipos} casoId={casoId} />
        ))}
      </div>
    </div>
  )
}

function DocumentoCard({ documento, tipos, casoId }: { documento: DocumentoDto; tipos: TipoDocumentoDto[]; casoId: string }) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [tipoId, setTipoId] = useState('')
  const [desde, setDesde] = useState('')
  const [hasta, setHasta] = useState('')
  const [downloading, setDownloading] = useState(false)

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
      const blob = await apiFetchBlob(casosApi.documentoContenidoPath(documento.id))
      const url = URL.createObjectURL(blob)
      const link = window.document.createElement('a')
      link.href = url
      link.download = documento.nombre
      link.click()
      URL.revokeObjectURL(url)
    } catch {
      showToast('error', 'No se pudo descargar el documento.')
    } finally {
      setDownloading(false)
    }
  }

  return (
    <Card className="flex flex-col gap-3 p-4">
      <div className="flex items-start justify-between gap-4">
        <div>
          <div className="font-medium text-gray-900">{documento.nombre}</div>
          <div className="text-xs text-gray-500">
            {formatBytes(documento.tamanoBytes)} · {documento.contentType} · {new Date(documento.createdAt).toLocaleString()}
          </div>
        </div>
        <button type="button" className="shrink-0 text-sm text-gray-500 hover:text-gray-900" disabled={downloading} onClick={handleDownload}>
          {downloading ? 'Descargando…' : 'Descargar'}
        </button>
      </div>

      <div className="flex flex-wrap gap-1">
        {documento.clasificaciones.length === 0 && <span className="text-xs text-gray-400">Sin clasificar</span>}
        {documento.clasificaciones.map((clasificacion) => (
          <span
            key={clasificacion.id}
            className="inline-flex items-center gap-1 rounded-full bg-gray-100 px-2 py-1 text-xs text-gray-700"
          >
            {clasificacion.tipoDocumentoNombre} · p. {clasificacion.paginaDesde}–{clasificacion.paginaHasta}
            <button
              type="button"
              className="text-gray-400 hover:text-red-600"
              disabled={removeMutation.isPending}
              onClick={() => removeMutation.mutate(clasificacion.id)}
            >
              ×
            </button>
          </span>
        ))}
      </div>

      {tipos.length > 0 && (
        <div className="flex flex-wrap items-end gap-2 border-t border-gray-100 pt-3">
          <div className="flex flex-col gap-1">
            <label className="text-xs text-gray-500">Tipo</label>
            <select
              className="rounded-lg border border-gray-300 px-2 py-1.5 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
              value={tipoId}
              onChange={(e) => setTipoId(e.target.value)}
            >
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
            <input
              type="number"
              min={1}
              className="w-20 rounded-lg border border-gray-300 px-2 py-1.5 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
              value={desde}
              onChange={(e) => setDesde(e.target.value)}
            />
          </div>
          <div className="flex flex-col gap-1">
            <label className="text-xs text-gray-500">Hasta</label>
            <input
              type="number"
              min={1}
              className="w-20 rounded-lg border border-gray-300 px-2 py-1.5 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
              value={hasta}
              onChange={(e) => setHasta(e.target.value)}
            />
          </div>
          <Button variant="ghost" disabled={!tipoId || !desde || !hasta || addMutation.isPending} onClick={() => addMutation.mutate()}>
            Añadir clasificación
          </Button>
        </div>
      )}
    </Card>
  )
}
