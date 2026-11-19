import { useCallback, useMemo } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import type { ColumnaDeTabla } from '../../../components/ui/DataTable'
import { BarraDeProgreso } from '../../../components/ui/BarraDeProgreso'
import { ListaDeDatos } from '../../../components/ui/ListaDeDatos'
import { PageHeader } from '../../../components/ui/PageHeader'
import { fechaHora } from '../../../lib/fechas'
import type { ConsultaDeLista } from '../../../lib/lista'
import { useListaPaginada } from '../../../lib/useListaPaginada'
import * as api from '../api'
import type { FamiliaDto } from '../api'
import { EstadoDeFamiliaBadge } from '../EstadoDeFamiliaBadge'

const ESTADOS = [
  { valor: '', etiqueta: 'Todas' },
  { valor: 'Abierta,Cerrada', etiqueta: 'Sin terminar' },
  { valor: 'Abierta', etiqueta: 'Abiertas' },
  { valor: 'Cerrada', etiqueta: 'Cerradas, esperando a sus casos' },
  { valor: 'Terminada', etiqueta: 'Terminadas' },
]

const obtenerId = (f: FamiliaDto) => f.id

/** The families of the person's processes: how far along each one is, and, once ended, how its cases ended. */
export function FamiliasPage() {
  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const flujoId = searchParams.get('flujoId') ?? undefined
  const tipoDeFamiliaId = searchParams.get('tipoDeFamiliaId') ?? undefined

  const cargar = useCallback(
    ({ busqueda, filtros, pagina, tamano }: ConsultaDeLista) =>
      api.listFamilias({
        flujoId,
        tipoDeFamiliaId,
        estado: filtros.estado,
        search: busqueda,
        page: String(pagina),
        pageSize: String(tamano),
      }),
    [flujoId, tipoDeFamiliaId],
  )

  const fuente = useListaPaginada<FamiliaDto>({
    clave: ['familias', flujoId, tipoDeFamiliaId],
    cargar,
    obtenerId,
    // Families still going move as their cases do.
    refrescarCada: (datos) => (datos?.items.some((f) => f.estado !== 'Terminada') ? 5000 : false),
  })

  const columnas = useMemo<ColumnaDeTabla<FamiliaDto>[]>(
    () => [
      {
        clave: 'familia',
        titulo: 'Familia',
        className: 'max-w-[18rem]',
        celda: (f) => (
          <span className="flex min-w-0 flex-col">
            <span className="truncate font-medium text-gray-900">{f.nombre}</span>
            <span className="truncate text-xs text-gray-500">
              {f.tipo} · {f.flujoNombre}
            </span>
          </span>
        ),
      },
      { clave: 'estado', titulo: 'Estado', celda: (f) => <EstadoDeFamiliaBadge estado={f.estado} /> },
      {
        clave: 'progreso',
        titulo: 'Casos',
        celda: (f) =>
          f.estado === 'Terminada' ? (
<span className="num text-sm text-gray-700">{api.describirFinal(f)}</span>
          ) : (
            <span className="flex w-48 flex-col gap-1">
              <BarraDeProgreso porcentaje={api.progreso(f)} etiqueta={`Avance de ${f.nombre}`} mostrarCifra={false} />
              <span className="text-xs text-gray-500">{api.describirProgreso(f)}</span>
            </span>
          ),
      },
      { clave: 'creada', titulo: 'Creada', celda: (f) => <span className="num text-sm text-gray-700">{fechaHora(f.createdAt)}</span> },
    ],
    [],
  )

  return (
    <div className="flex flex-col gap-4">
      <PageHeader
        title="Familias"
        description="Grupos de casos que acaban juntos. Cuando una familia cerrada ve terminar todos sus casos, hace lo que diga su tipo: abrir un caso de cierre o avisar."
      />
      <ListaDeDatos
        fuente={fuente}
        columnas={columnas}
        obtenerId={obtenerId}
        nombreDeFila={(f) => f.nombre}
        entidad={{ singular: 'familia', plural: 'familias' }}
        buscador={{ etiqueta: 'Buscar por nombre', placeholder: 'Buscar por nombre…' }}
        filtros={[{ clave: 'estado', etiqueta: 'Estado', opciones: ESTADOS }]}
        vacio={{
          titulo: 'No hay familias',
          descripcion: 'Las crean los robots (o el API) al abrir casos que van juntos. Sus tipos se definen en cada proceso, pestaña «Familias».',
        }}
        alHacerClic={(f) => navigate(`/familias/${f.id}`)}
        anchoMinimo="48rem"
      />
    </div>
  )
}
