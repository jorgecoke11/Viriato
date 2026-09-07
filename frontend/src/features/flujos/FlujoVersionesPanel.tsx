import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { Modal } from '../../components/ui/Modal'
import { ApiError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import { FlujoPasosEditor } from './FlujoPasosEditor'
import * as flujosApi from './api'
import type { FlujoVersionDto } from './api'

const estadoBadge: Record<FlujoVersionDto['estado'], string> = {
  Borrador: 'bg-gray-100 text-gray-600',
  Publicada: 'bg-green-100 text-green-700',
  Archivada: 'bg-amber-100 text-amber-700',
}

export function FlujoVersionesPanel({ flujoId }: { flujoId: string }) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [showNueva, setShowNueva] = useState(false)
  const [notas, setNotas] = useState('')
  const [editingVersionId, setEditingVersionId] = useState<string | null>(null)
  const [pendingPublicar, setPendingPublicar] = useState<FlujoVersionDto | null>(null)
  const [pendingArchivar, setPendingArchivar] = useState<FlujoVersionDto | null>(null)

  const versionesQuery = useQuery({
    queryKey: [`flujo-${flujoId}-versiones`],
    queryFn: () => flujosApi.listFlujoVersiones(flujoId),
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: [`flujo-${flujoId}-versiones`] })

  const createMutation = useMutation({
    mutationFn: () => flujosApi.createFlujoVersion(flujoId, { notas: notas.trim() || null }),
    onSuccess: (version) => {
      invalidate()
      setShowNueva(false)
      setNotas('')
      setEditingVersionId(version.id)
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo crear la versión.'),
  })

  const publicarMutation = useMutation({
    mutationFn: (versionId: string) => flujosApi.publicarFlujoVersion(flujoId, versionId),
    onSuccess: () => {
      invalidate()
      queryClient.invalidateQueries({ queryKey: ['flujo', flujoId] })
      setPendingPublicar(null)
      showToast('success', 'Versión publicada.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo publicar la versión.'),
  })

  const archivarMutation = useMutation({
    mutationFn: (versionId: string) => flujosApi.archivarFlujoVersion(flujoId, versionId),
    onSuccess: () => {
      invalidate()
      setPendingArchivar(null)
      showToast('success', 'Versión archivada.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo archivar la versión.'),
  })

  const versiones = [...(versionesQuery.data ?? [])].sort((a, b) => b.numeroVersion - a.numeroVersion)

  if (editingVersionId) {
    return <FlujoPasosEditor flujoId={flujoId} versionId={editingVersionId} onClose={() => setEditingVersionId(null)} />
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-gray-500">Cada versión es un snapshot inmutable de los pasos una vez publicada.</p>
        <Button onClick={() => setShowNueva(true)}>Nueva versión</Button>
      </div>

      <Card className="overflow-x-auto p-0">
        <table className="w-full min-w-[640px] text-left text-sm">
          <thead className="border-b border-gray-200 text-gray-500">
            <tr>
              <th className="px-4 py-3 font-medium">Nº</th>
              <th className="px-4 py-3 font-medium">Estado</th>
              <th className="px-4 py-3 font-medium">Notas</th>
              <th className="px-4 py-3 font-medium">Creada</th>
              <th className="px-4 py-3 font-medium">Publicada</th>
              <th className="px-4 py-3 font-medium">Acciones</th>
            </tr>
          </thead>
          <tbody>
            {versiones.map((version) => (
              <tr key={version.id} className="border-b border-gray-100 last:border-0">
                <td className="px-4 py-3">v{version.numeroVersion}</td>
                <td className="px-4 py-3">
                  <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${estadoBadge[version.estado]}`}>
                    {version.estado}
                  </span>
                </td>
                <td className="px-4 py-3 text-gray-500">{version.notas ?? '—'}</td>
                <td className="px-4 py-3 text-gray-500">{new Date(version.createdAt).toLocaleString()}</td>
                <td className="px-4 py-3 text-gray-500">{version.publishedAt ? new Date(version.publishedAt).toLocaleString() : '—'}</td>
                <td className="px-4 py-3">
                  <div className="flex items-center gap-3">
                    <button type="button" className="text-gray-500 hover:text-gray-900" onClick={() => setEditingVersionId(version.id)}>
                      {version.estado === 'Borrador' ? 'Editar pasos' : 'Ver pasos'}
                    </button>
                    {version.estado === 'Borrador' && (
                      <button
                        type="button"
                        className="text-indigo-600 hover:text-indigo-800"
                        onClick={() => setPendingPublicar(version)}
                      >
                        Publicar
                      </button>
                    )}
                    {version.estado !== 'Archivada' && (
                      <button
                        type="button"
                        className="text-gray-500 hover:text-red-600"
                        onClick={() => setPendingArchivar(version)}
                      >
                        Archivar
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {versionesQuery.isLoading && <p className="p-4 text-sm text-gray-500">Cargando…</p>}
        {versionesQuery.isSuccess && versiones.length === 0 && <p className="p-4 text-sm text-gray-500">Sin versiones todavía.</p>}
      </Card>

      <Modal
        open={showNueva}
        title="Nueva versión"
        onClose={() => setShowNueva(false)}
        footer={
          <div className="flex justify-end gap-2">
            <Button type="button" variant="ghost" onClick={() => setShowNueva(false)}>
              Cancelar
            </Button>
            <Button type="button" disabled={createMutation.isPending} onClick={() => createMutation.mutate()}>
              {createMutation.isPending ? 'Creando…' : 'Crear'}
            </Button>
          </div>
        }
      >
        <div className="flex flex-col gap-1">
          <label className="text-sm font-medium text-gray-700">Notas (opcional)</label>
          <textarea
            className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
            rows={3}
            value={notas}
            onChange={(e) => setNotas(e.target.value)}
          />
        </div>
      </Modal>

      <ConfirmDialog
        open={pendingPublicar !== null}
        title="Publicar versión"
        message={`¿Publicar la versión v${pendingPublicar?.numeroVersion}? Pasará a ser la versión activa del proceso: los nuevos casos la usarán y sus pasos quedarán congelados.`}
        confirmLabel="Publicar"
        pendingLabel="Publicando…"
        pending={publicarMutation.isPending}
        onConfirm={() => pendingPublicar && publicarMutation.mutate(pendingPublicar.id)}
        onCancel={() => setPendingPublicar(null)}
      />

      <ConfirmDialog
        open={pendingArchivar !== null}
        title="Archivar versión"
        message={`¿Archivar la versión v${pendingArchivar?.numeroVersion}? Dejará de poder editarse o publicarse.`}
        confirmLabel="Archivar"
        pendingLabel="Archivando…"
        pending={archivarMutation.isPending}
        onConfirm={() => pendingArchivar && archivarMutation.mutate(pendingArchivar.id)}
        onCancel={() => setPendingArchivar(null)}
      />
    </div>
  )
}
