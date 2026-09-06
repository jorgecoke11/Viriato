import { useQuery } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { Modal } from '../../components/ui/Modal'
import * as casosApi from './api'
import { CasoEstadoBadge } from './CasoEstadoBadge'
import type { TipoCasoFiltro } from './ProcesoResumenCard'

export interface TipoCasoModalData {
  flujoId: string
  flujoNombre: string
  tipoCasoId: string | null
  tipoCasoNombre: string
  filtro: TipoCasoFiltro
}

// Quick peek from the dashboard's accordion — only ever lists casos here. The actual case detail
// always lives on its own screen (/casos/:id), never embedded in a modal, so it gets full room to breathe.
//
// Stays mounted permanently (see `open`/Modal) so its close animation can play — `Modal` itself
// freezes the title/content it displays while animating out, so the null-safety below only needs
// to stop this render from throwing, not keep the exiting panel looking right.
export function TipoCasoModal({ data, onClose }: { data: TipoCasoModalData | null; onClose: () => void }) {
  const navigate = useNavigate()
  const open = data !== null

  const query = useQuery({
    queryKey: ['casos', data?.flujoId, data?.tipoCasoId, data?.filtro, 'modal'],
    queryFn: () =>
      casosApi.listCasos({
        flujoId: data!.flujoId,
        tipoCasoId: data!.tipoCasoId ?? 'sin-tipo',
        finalizado: data!.filtro.kind === 'bucket' ? data!.filtro.bucket === 'finalizado' : undefined,
        estadoNegocioCodigo: data!.filtro.kind === 'estado' ? (data!.filtro.codigo ?? 'sin-estado') : undefined,
        pageSize: 50,
      }),
    enabled: open,
  })

  const subtitulo = data
    ? data.filtro.kind === 'bucket'
      ? data.filtro.bucket === 'finalizado'
        ? 'Finalizados'
        : 'En curso'
      : data.filtro.display
    : ''

  return (
    <Modal open={open} title={data ? `${data.flujoNombre} · ${data.tipoCasoNombre} · ${subtitulo}` : ''} onClose={onClose}>
      <div className="flex flex-col gap-2">
        {query.isLoading && <p className="text-sm text-gray-500">Cargando…</p>}
        {query.data?.items.length === 0 && <p className="text-sm text-gray-500">No hay casos en este grupo.</p>}
        {query.data?.items.map((caso) => (
          <button
            key={caso.id}
            className="flex items-center justify-between rounded-md border border-gray-100 px-3 py-2 text-left text-sm hover:bg-gray-50"
            onClick={() => navigate(`/casos/${caso.id}`)}
          >
            <span className="font-medium text-gray-900">{caso.titulo}</span>
            <span className="flex items-center gap-3 text-gray-400">
              {new Date(caso.createdAt).toLocaleString()}
              <CasoEstadoBadge estado={caso.estadoNegocio?.display ?? caso.estado} />
            </span>
          </button>
        ))}
      </div>
    </Modal>
  )
}
