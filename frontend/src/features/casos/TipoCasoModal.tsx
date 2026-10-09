import { CalendarRange } from 'lucide-react'
import { useCallback, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { ListaDeDatos, type FiltroDeLista } from '../../components/ui/ListaDeDatos'
import { Modal } from '../../components/ui/Modal'
import type { ConsultaDeLista } from '../../lib/lista'
import { useListaPaginada } from '../../lib/useListaPaginada'
import * as casosApi from './api'
import type { CasoListItemDto, EstadoConteoDto } from './api'
import { describeFiltro, filtroToListParams, type FinalizadosFiltro } from './finalizadosFiltro'
import type { TipoCasoFiltro } from './ProcesoResumenCard'
import { GRUPOS, opcionesDeSituacion, valorDeGrupo } from './situaciones'
import { useColumnasDeCasos } from './useColumnasDeCasos'

export interface TipoCasoModalData {
  flujoId: string
  flujoNombre: string
  tipoCasoId: string | null
  tipoCasoNombre: string
  filtro: TipoCasoFiltro
  /** The finished-Casos window the dashboard was counting with when this group was opened. */
  modo: FinalizadosFiltro
  /** The business states of the type, with how many Casos each has — to filter the group by one of them. */
  estados: EstadoConteoDto[]
}

const obtenerId = (caso: CasoListItemDto) => caso.id

// Quick peek from the dashboard's accordion — only ever lists casos here. The actual case detail
// always lives on its own screen (/casos/:id), never embedded in a modal, so it gets full room to breathe.
//
// It lists exactly what the dashboard counted: the Casos still moving, plus the finished ones inside the window the
// dashboard was using (the card's own, or the panel's). On top of that it can be narrowed down — by title, by how the
// Caso stands within the group, and by one of its business states — and paged through, all of it through the same list
// (`ListaDeDatos`) every other list of Casos uses. The group (running, pending, stopped, finished) is what was clicked; the
// business state is a different question, so it is offered as its own filter.
//
// Stays mounted permanently (see `open`/Modal) so its close animation can play — `Modal` itself
// freezes the title/content it displays while animating out, so the null-safety below only needs
// to stop this render from throwing, not keep the exiting panel looking right.
export function TipoCasoModal({ data, onClose }: { data: TipoCasoModalData | null; onClose: () => void }) {
  const open = data !== null
  const navigate = useNavigate()
  const columnas = useColumnasDeCasos()

  const grupo = data?.filtro.kind === 'grupo' ? data.filtro.grupo : null
  // Counts are of the whole type, not of this group, so they are left out: the list says how many match.
  const estadosDeNegocio = grupo === null ? [] : (data?.estados ?? [])

  const cargar = useCallback(
    ({ busqueda, filtros, pagina, tamano }: ConsultaDeLista) =>
      casosApi.listCasos({
        flujoId: data!.flujoId,
        tipoCasoId: data!.tipoCasoId ?? 'sin-tipo',
        estadoNegocioCodigo: data!.filtro.kind === 'estado' ? (data!.filtro.codigo ?? 'sin-estado') : filtros.estadoNegocio || undefined,
        estado: filtros.situacion || (grupo ? valorDeGrupo(grupo) : undefined),
        search: busqueda || undefined,
        ...filtroToListParams(data!.modo),
        page: pagina,
        pageSize: tamano,
      }),
    [data, grupo],
  )

  const fuente = useListaPaginada<CasoListItemDto>({
    clave: ['casos', 'modal', data?.flujoId, data?.tipoCasoId, data?.filtro, data?.modo],
    cargar,
    obtenerId,
    habilitada: open,
  })

  // Every time a group is opened it starts unfiltered.
  const { quitarFiltros, setPagina } = fuente
  useEffect(() => {
    quitarFiltros()
    setPagina(1)
  }, [data, quitarFiltros, setPagina])

  const filtros: FiltroDeLista[] = [
    { clave: 'situacion', etiqueta: 'Situación del caso', opciones: opcionesDeSituacion(grupo).map((o) => ({ valor: o.valor, etiqueta: o.etiqueta })) },
    ...(estadosDeNegocio.length > 1
      ? [
          {
            clave: 'estadoNegocio',
            etiqueta: 'Estado de negocio',
            opciones: [{ valor: '', etiqueta: 'Cualquier estado' }, ...estadosDeNegocio.map((e) => ({ valor: e.codigo ?? 'sin-estado', etiqueta: e.display }))],
          },
        ]
      : []),
  ]

  const subtitulo = data ? (data.filtro.kind === 'grupo' ? GRUPOS[data.filtro.grupo].etiqueta : data.filtro.display) : ''

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
          Los casos en curso siempre se ven; {describeFiltro(data.modo).toLowerCase()}.
        </p>
      )}

      <ListaDeDatos
        fuente={fuente}
        columnas={columnas}
        obtenerId={obtenerId}
        nombreDeFila={(caso) => caso.titulo}
        entidad={{ singular: 'caso', plural: 'casos' }}
        buscador={{ etiqueta: 'Buscar por título', placeholder: 'Buscar por título…' }}
        filtros={filtros}
        vacio={{ titulo: 'No hay casos en este grupo' }}
        alHacerClic={(caso) => navigate(`/casos/${caso.id}`)}
        anchoMinimo="54rem"
      />
    </Modal>
  )
}
