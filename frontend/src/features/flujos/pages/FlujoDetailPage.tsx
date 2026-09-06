import { useQuery } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { Card } from '../../../components/ui/Card'
import { Tabs } from '../../../components/ui/Tabs'
import { duration, ease } from '../../../lib/motion/tokens'
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
