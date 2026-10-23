import { useQuery } from '@tanstack/react-query'
import { Eye } from 'lucide-react'
import { useState } from 'react'
import { CrudPage } from '../../components/crud/CrudPage'
import type { CrudColumn } from '../../components/crud/types'
import { IconButton } from '../../components/ui/IconButton'
import { Modal } from '../../components/ui/Modal'
import { VisorDeDatos } from '../../components/ui/VisorDeDatos'
import * as flujosApi from './api'
import type { DatoDeProcesoResumenDto } from './api'

const LIMITE = 64 * 1024

const fechaHora = (iso: string) => new Date(iso).toLocaleString('es-ES', { dateStyle: 'short', timeStyle: 'short' })

function tamano(bytes: number) {
  return bytes < 1024 ? `${bytes} B` : `${(bytes / 1024).toLocaleString('es-ES', { maximumFractionDigits: 1 })} KB`
}

/** The value of one key, read when it is opened (a key can hold up to 64 KB, so the list does not bring them). */
function ContenidoDelDato({ flujoId, clave }: { flujoId: string; clave: string }) {
  const query = useQuery({ queryKey: ['flujo-almacen-dato', flujoId, clave], queryFn: () => flujosApi.getDatoDelProceso(flujoId, clave) })

  if (query.isLoading) return <p className="text-sm text-gray-500">Cargando…</p>
  if (query.isError || !query.data) return <p className="text-sm text-red-600">No se pudo leer este dato.</p>
  return (
    <VisorDeDatos
      titulo={<span className="font-mono text-xs">{clave}</span>}
      json={query.data.valorJson}
      textoVacio="Está vacío."
    />
  )
}

/**
 * What the process's robots keep between cases (its store): the history of a product's price, the last page read… Read-only — the
 * robots own it, and a value changed by hand would only confuse the next case. Each key opens in a readable view.
 */
export function FlujoAlmacenPanel({ flujoId }: { flujoId: string }) {
  const [abierto, setAbierto] = useState<string | null>(null)

  const columns: CrudColumn<DatoDeProcesoResumenDto>[] = [
    { key: 'clave', label: 'Clave', render: (d) => <span className="break-all font-mono text-xs text-gray-800">{d.clave}</span> },
    {
      key: 'tamano',
      label: 'Tamaño',
      render: (d) => (
        <span className={`num text-xs ${d.bytes > LIMITE * 0.8 ? 'text-amber-700' : 'text-gray-600'}`} title="Cada clave admite hasta 64 KB">
          {tamano(d.bytes)}
        </span>
      ),
    },
    { key: 'actualizado', label: 'Actualizado', render: (d) => <span className="num text-gray-600">{fechaHora(d.actualizadoEn)}</span> },
    {
      key: 'ver',
      label: '',
      render: (d) => (
        <IconButton label={`Ver ${d.clave}`} size="sm" onClick={() => setAbierto(d.clave)}>
          <Eye size={16} />
        </IconButton>
      ),
    },
  ]

  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm text-gray-500">
        Lo que los robots de este proceso recuerdan de un caso a otro (por ejemplo, el historial de precios de cada producto). Solo se puede consultar: lo
        escriben los robots.
      </p>
      <CrudPage<DatoDeProcesoResumenDto, Record<string, string>, never, never>
        title="Almacén"
        resourceKey={`flujo-${flujoId}-almacen`}
        getId={(d) => d.clave}
        columns={columns}
        filters={{ mode: 'general', placeholder: 'Clave…' }}
        entidad={{ singular: 'dato', plural: 'datos' }}
        nombreDeFila={(d) => d.clave}
        api={{
          list: async (filters) => {
            const term = filters.search?.toLowerCase() ?? ''
            const todos = await flujosApi.listAlmacenDelProceso(flujoId)
            const filtrados = todos.filter((d) => !term || d.clave.toLowerCase().includes(term))
            return { items: filtrados, page: 1, pageSize: filtrados.length || 1, total: filtrados.length }
          },
        }}
      />
      <Modal open={abierto !== null} size="lg" title="Dato del almacén" onClose={() => setAbierto(null)}>
        {abierto && <ContenidoDelDato flujoId={flujoId} clave={abierto} />}
      </Modal>
    </div>
  )
}
