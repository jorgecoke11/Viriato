import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Button } from '../../../components/ui/Button'
import { Card } from '../../../components/ui/Card'
import { Tabs } from '../../../components/ui/Tabs'
import { ApiError } from '../../../lib/apiClient'
import { duration, ease } from '../../../lib/motion/tokens'
import { useToast } from '../../../lib/toast/useToast'
import * as flujosApi from '../api'
import { FlujoEstadosPanel } from '../FlujoEstadosPanel'
import { FlujoTiposCasoPanel } from '../FlujoTiposCasoPanel'

type Tab = 'estados' | 'tipos'

export function FlujoDetailPage() {
  const { id } = useParams<{ id: string }>()
  const [tab, setTab] = useState<Tab>('estados')
  const query = useQuery({ queryKey: ['flujo', id], queryFn: () => flujosApi.getFlujo(id!), enabled: Boolean(id) })

  if (!id) return null
  if (query.isLoading) return <p className="text-gray-500">Cargando…</p>
  if (query.isError || !query.data) return <p className="text-gray-500">Proceso no encontrado.</p>

  const flujo = query.data

  return (
    <div className="flex flex-col gap-4">
      <Link to="/admin/flujos" className="text-sm text-gray-500 hover:text-gray-900">← Procesos</Link>

      <Card>
        <h1 className="text-xl font-semibold text-gray-900">{flujo.nombre}</h1>
        {flujo.descripcion && <p className="text-sm text-gray-500">{flujo.descripcion}</p>}
      </Card>

      <Card>
        <FlujoAlmacenamientoField flujo={flujo} />
      </Card>

      <div>
        <Tabs
          tabs={[
            { value: 'estados', label: 'Estados' },
            { value: 'tipos', label: 'Tipos de caso' },
          ]}
          active={tab}
          onChange={setTab}
        />

        <AnimatePresence mode="wait">
          {tab === 'estados' && (
            <motion.div
              key="estados"
              initial={{ opacity: 0, y: 4 }}
              animate={{ opacity: 1, y: 0, transition: { duration: duration.fast, ease: ease.out } }}
              exit={{ opacity: 0, transition: { duration: duration.fast, ease: ease.in } }}
            >
              <Card className="mt-4">
                <FlujoEstadosPanel flujoId={flujo.id} />
              </Card>
            </motion.div>
          )}

          {tab === 'tipos' && (
            <motion.div
              key="tipos"
              initial={{ opacity: 0, y: 4 }}
              animate={{ opacity: 1, y: 0, transition: { duration: duration.fast, ease: ease.out } }}
              exit={{ opacity: 0, transition: { duration: duration.fast, ease: ease.in } }}
            >
              <Card className="mt-4">
                <FlujoTiposCasoPanel flujoId={flujo.id} />
              </Card>
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </div>
  )
}

function FlujoAlmacenamientoField({ flujo }: { flujo: flujosApi.FlujoDto }) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [selected, setSelected] = useState(flujo.storageConfigId ?? '')

  useEffect(() => {
    setSelected(flujo.storageConfigId ?? '')
  }, [flujo.storageConfigId])

  const storageConfigsQuery = useQuery({
    queryKey: ['storage-configs-all'],
    queryFn: () => flujosApi.listStorageConfigs(),
  })

  const saveMutation = useMutation({
    mutationFn: (storageConfigId: string | null) => flujosApi.updateFlujoAlmacenamiento(flujo.id, storageConfigId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['flujo', flujo.id] })
      showToast('success', 'Almacenamiento actualizado.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo actualizar el almacenamiento.'),
  })

  const storageConfigs = storageConfigsQuery.data?.items ?? []
  const hasChanges = selected !== (flujo.storageConfigId ?? '')

  return (
    <div className="flex flex-col gap-2">
      <label className="text-sm font-medium text-gray-700">Almacenamiento</label>
      <p className="text-xs text-gray-500">
        Dónde se guardan las evidencias (capturas/vídeos del RPA) y los documentos de los casos de este proceso.
      </p>
      <div className="flex items-center gap-2">
        <select
          className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
          value={selected}
          onChange={(e) => setSelected(e.target.value)}
        >
          <option value="">Sin asignar</option>
          {storageConfigs.map((config) => (
            <option key={config.id} value={config.id}>
              {config.nombre}
            </option>
          ))}
        </select>
        <Button
          variant="ghost"
          disabled={!hasChanges || saveMutation.isPending}
          onClick={() => saveMutation.mutate(selected || null)}
        >
          Guardar
        </Button>
      </div>
    </div>
  )
}
