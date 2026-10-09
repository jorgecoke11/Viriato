import { useQuery } from '@tanstack/react-query'
import { useCallback, useMemo } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { Button } from '../../../components/ui/Button'
import { EmptyState } from '../../../components/ui/EmptyState'
import { ListaDeDatos } from '../../../components/ui/ListaDeDatos'
import { MultiSelect } from '../../../components/ui/MultiSelect'
import { PageHeader } from '../../../components/ui/PageHeader'
import { SelectorDeRango } from '../../../components/ui/SelectorDeRango'
import type { ConsultaDeLista } from '../../../lib/lista'
import { esRangoElegido, finDelDia, inicioDelDia, rangoAParam, rangoDeParam, resolverRango, type RangoElegido } from '../../../lib/rangoDeFechas'
import { useListaPaginada } from '../../../lib/useListaPaginada'
import { useAuth } from '../../auth/useAuth'
import { usePreferencia } from '../../auth/usePreferencia'
import { useSeleccionPorExclusion } from '../../auth/useSeleccionPorExclusion'
import { accionesMasivasDeCasos } from '../accionesMasivas'
import * as casosApi from '../api'
import type { CasoListItemDto } from '../api'
import { GRUPOS, valorDeGrupo } from '../situaciones'
import { useColumnasDeCasos } from '../useColumnasDeCasos'

const ESTADOS = [
  { valor: '', etiqueta: 'Todas las situaciones' },
  { valor: 'Iniciado', etiqueta: 'Iniciado' },
  { valor: 'Pendiente', etiqueta: 'Pendiente (en cola)' },
  { valor: 'EnProgreso', etiqueta: 'En ejecución' },
  { valor: 'Pausado', etiqueta: 'Pausado' },
  { valor: 'EsperandoRevisionHumana', etiqueta: 'Esperando revisión' },
  { valor: valorDeGrupo('detenido'), etiqueta: `${GRUPOS.detenido.etiqueta} (iniciados, pausados o en revisión)` },
  { valor: 'Completado', etiqueta: 'Completado' },
  { valor: 'Fallido', etiqueta: 'Fallido' },
  { valor: 'Cancelado', etiqueta: 'Cancelado' },
]

/** What a list of Casos shows until someone chooses otherwise: the recent ones, so it never starts as the whole history. */
const FECHAS_POR_DEFECTO: RangoElegido = { tipo: 'atajo', atajo: '30d' }

const obtenerId = (caso: CasoListItemDto) => caso.id

/**
 * The one screen for Casos: the list, with everything you can do to them. Search, process, situation and creation date narrow it
 * down; it is paged; and — for whoever may use the bulk actions — every row can be ticked (also all that match, past the page) to
 * apply the actions of `accionesMasivasDeCasos`: cancel, reprocess, download documents. The processes are picked with the same
 * `MultiSelect` as the dashboard's "Personalizar": all of them to start with, take away the ones you do not want (kept for your
 * user). A link that says `?flujoId=` (the dashboard's "Ver todos los casos") starts with only that process.
 */
export function CasosListPage() {
  const navigate = useNavigate()
  const { can } = useAuth()
  const [searchParams, setSearchParams] = useSearchParams()
  const flujoDelEnlace = searchParams.get('flujoId')

  // When the Casos were created. The link the person came from can say (`?fechas=`); otherwise it is what they chose last time.
  const [recordadas, setRecordadas] = usePreferencia<RangoElegido>('casos:lista:fechas', FECHAS_POR_DEFECTO, esRangoElegido)
  const fechas = rangoDeParam(searchParams.get('fechas')) ?? recordadas
  const { desde, hasta } = resolverRango(fechas)

  const flujosQuery = useQuery({ queryKey: ['flujos-asignados'], queryFn: casosApi.listFlujosAsignados })
  const procesos = useMemo(() => [...(flujosQuery.data ?? [])].sort((a, b) => a.nombre.localeCompare(b.nombre, 'es')), [flujosQuery.data])
  const idsDeProcesos = useMemo(() => procesos.map((p) => p.id), [procesos])

  const forzada = useMemo(() => (flujoDelEnlace ? new Set([flujoDelEnlace]) : null), [flujoDelEnlace])
  const { elegidos, cambiar } = useSeleccionPorExclusion('casos:lista:procesos-excluidos', idsDeProcesos, forzada)
  const todosElegidos = elegidos.size === idsDeProcesos.length

  const columnas = useColumnasDeCasos()
  const acciones = can('casos.masivas') ? accionesMasivasDeCasos.filter((a) => !a.permiso || can(a.permiso)) : []
  const claveDeProcesos = [...elegidos].sort().join(',')

  const cargar = useCallback(
    async ({ busqueda, filtros, pagina, tamano }: ConsultaDeLista) => {
      // Nothing chosen is nothing to show (not "everything").
      if (elegidos.size === 0) return { items: [], page: 1, pageSize: tamano, total: 0 }
      return casosApi.listCasos({
        // All of them is no filter at all: the server already limits to the processes the person is assigned to.
        flujoIds: todosElegidos ? undefined : [...elegidos].join(','),
        estado: filtros.estado || undefined,
        search: busqueda || undefined,
        desde: desde ? inicioDelDia(desde) : undefined,
        hasta: hasta ? finDelDia(hasta) : undefined,
        page: pagina,
        pageSize: tamano,
      })
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [claveDeProcesos, todosElegidos, desde, hasta],
  )

  const filtrosPorDefecto = useMemo(() => ({ estado: searchParams.get('estado') ?? '' }), [searchParams])

  const fuente = useListaPaginada<CasoListItemDto>({
    clave: ['casos', 'lista', claveDeProcesos, desde, hasta],
    cargar,
    obtenerId,
    filtrosPorDefecto,
    // A list with something moving keeps itself up to date.
    refrescarCada: (datos) => (datos?.items.some((c) => c.estado === 'EnProgreso' || c.estado === 'Pendiente') ? 3000 : false),
  })

  function cambiarFechas(nuevas: RangoElegido) {
    setRecordadas(nuevas)
    const parametros = new URLSearchParams(searchParams)
    parametros.set('fechas', rangoAParam(nuevas))
    setSearchParams(parametros, { replace: true })
    fuente.setPagina(1)
  }

  function cambiarProcesos(nuevos: Set<string>) {
    // From now on the choice is the person's, not the link's.
    if (flujoDelEnlace) {
      const parametros = new URLSearchParams(searchParams)
      parametros.delete('flujoId')
      setSearchParams(parametros, { replace: true })
    }
    cambiar(nuevos)
    fuente.setPagina(1)
  }

  return (
    <div className="flex flex-col gap-4">
      <PageHeader
        title="Casos"
        description={
          acciones.length > 0
            ? 'Busca, filtra y marca casos para actuar sobre varios a la vez: cancelarlos, reprocesarlos o descargar sus documentos.'
            : 'Busca y filtra los casos de tus procesos.'
        }
        actions={can('casos.crear') ? <Button onClick={() => navigate('/casos/nuevo')}>Nuevo caso</Button> : undefined}
      />

      {flujosQuery.isLoading ? (
        <p className="text-gray-500">Cargando…</p>
      ) : procesos.length === 0 ? (
        <EmptyState title="No tienes ningún proceso asignado" description="Cuando te asignen uno, verás aquí sus casos." />
      ) : (
        <>
          <div className="max-w-xl">
            <MultiSelect
              etiqueta="Procesos"
              opciones={procesos.map((p) => ({ id: p.id, etiqueta: p.nombre }))}
              seleccion={elegidos}
              alCambiar={cambiarProcesos}
              plural="procesos"
              singular="proceso"
              ayuda="Se ven todos. Abre la lista para quitar los que no quieras ver."
            />
          </div>

          <ListaDeDatos
            fuente={fuente}
            columnas={columnas}
            obtenerId={obtenerId}
            nombreDeFila={(caso) => caso.titulo}
            entidad={{ singular: 'caso', plural: 'casos' }}
            buscador={{ etiqueta: 'Buscar por título', placeholder: 'Buscar por título…' }}
            filtros={[{ clave: 'estado', etiqueta: 'Situación del caso', opciones: ESTADOS }]}
            barraExtra={
              <SelectorDeRango
                titulo="Fechas de creación"
                valor={fechas}
                alCambiar={cambiarFechas}
                textoDeTodos="Todas las fechas"
                encabezado="Se filtran los casos por la fecha en que se crearon."
              />
            }
            acciones={acciones}
            vacio={{
              titulo: 'No hay casos',
              descripcion: elegidos.size === 0 ? 'No hay ningún proceso elegido: marca alguno arriba.' : 'No hay casos con estos filtros. Prueba con otro rango de fechas o con otros procesos.',
            }}
            alHacerClic={(caso) => navigate(`/casos/${caso.id}`)}
            anchoMinimo="54rem"
          />
        </>
      )}
    </div>
  )
}
