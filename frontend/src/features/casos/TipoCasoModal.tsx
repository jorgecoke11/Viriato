import { useQuery } from '@tanstack/react-query'
import { CalendarRange } from 'lucide-react'
import { Modal } from '../../components/ui/Modal'
import { Skeleton } from '../../components/ui/Skeleton'
import * as casosApi from './api'
import { CasosTabla } from './CasosTabla'
import { describeFiltro, filtroToListParams, type FinalizadosFiltro } from './finalizadosFiltro'
import type { TipoCasoFiltro } from './ProcesoResumenCard'

export interface TipoCasoModalData {
  flujoId: string
  flujoNombre: string
  tipoCasoId: string | null
  tipoCasoNombre: string
  filtro: TipoCasoFiltro
  /** The finished-Casos window the dashboard was counting with when this group was opened. */
  modo: FinalizadosFiltro
}

const PAGE_SIZE = 50

// Quick peek from the dashboard's accordion — only ever lists casos here. The actual case detail
// always lives on its own screen (/casos/:id), never embedded in a modal, so it gets full room to breathe.
//
// It lists exactly what the dashboard counted: the Casos still moving, plus the finished ones inside the window the
// dashboard was using (the card's own, or the panel's). Asking for the group alone would list every Caso it ever had.
//
// Stays mounted permanently (see `open`/Modal) so its close animation can play — `Modal` itself
// freezes the title/content it displays while animating out, so the null-safety below only needs
// to stop this render from throwing, not keep the exiting panel looking right.
export function TipoCasoModal({ data, onClose }: { data: TipoCasoModalData | null; onClose: () => void }) {
  const open = data !== null

  const query = useQuery({
    queryKey: ['casos', data?.flujoId, data?.tipoCasoId, data?.filtro, data?.modo, 'modal'],
    queryFn: () =>
      casosApi.listCasos({
        flujoId: data!.flujoId,
        tipoCasoId: data!.tipoCasoId ?? 'sin-tipo',
        finalizado: data!.filtro.kind === 'bucket' ? data!.filtro.bucket === 'finalizado' : undefined,
        estadoNegocioCodigo: data!.filtro.kind === 'estado' ? (data!.filtro.codigo ?? 'sin-estado') : undefined,
        ...filtroToListParams(data!.modo),
        pageSize: PAGE_SIZE,
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

  const total = query.data?.total ?? 0

  return (
    <Modal
      open={open}
      size="xl"
      title={
        data ? (
          <span className="flex flex-wrap items-baseline gap-x-2">
            <span>{data.tipoCasoNombre}</span>
            <span className="text-sm font-normal text-gray-500">
              {data.flujoNombre} · {subtitulo}
            </span>
          </span>
        ) : (
          ''
        )
      }
      onClose={onClose}
    >
      {data && (
        <p className="mb-3 flex items-center gap-1.5 text-xs text-gray-500">
          <CalendarRange size={13} aria-hidden="true" />
          Los casos en curso siempre se ven; finalizados: {describeFiltro(data.modo).toLowerCase()}.
        </p>
      )}

      {query.isLoading && (
        <div role="status" aria-label="Cargando casos" className="flex flex-col gap-2">
          <Skeleton className="h-10" />
          <Skeleton className="h-14" />
          <Skeleton className="h-14" />
        </div>
      )}
      {query.isError && <p className="text-sm text-red-600">No se han podido cargar los casos.</p>}
      {query.data?.items.length === 0 && <p className="py-6 text-center text-sm text-gray-500">No hay casos en este grupo.</p>}

      {query.data && query.data.items.length > 0 && (
        <div className="-mx-5 border-y border-gray-200">
          <CasosTabla casos={query.data.items} />
        </div>
      )}

      {query.data && total > query.data.items.length && (
        <p className="mt-3 text-xs text-gray-500">
          Mostrando los {query.data.items.length} más recientes de {total}.
        </p>
      )}
    </Modal>
  )
}
