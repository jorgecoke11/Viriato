import { useQuery } from '@tanstack/react-query'
import { useMemo } from 'react'
import { Link } from 'react-router-dom'
import { CrudPage } from '../../components/crud/CrudPage'
import type { CrudColumn, CrudField, CrudFormConfig } from '../../components/crud/types'
import { erroresDeListaDeDatos } from '../../components/schema-form/comprobar'
import { ListaDeDatosConFormulario } from '../../components/schema-form/ListaDeDatosConFormulario'
import { Badge } from '../../components/ui/Badge'
import * as flujosApi from './api'
import type { LanzadorDeCasoDto } from './api'

interface LanzadorFormValues {
  [key: string]: string | boolean
  nombre: string
  descripcion: string
  creadorDeCasoId: string
  datosJson: string
  cron: string
  zonaHoraria: string
  toleranciaMinutos: string
  activo: boolean
}

type Config = CrudFormConfig<LanzadorDeCasoDto, LanzadorFormValues, flujosApi.GuardarLanzadorDeCasoRequest, flujosApi.GuardarLanzadorDeCasoRequest>

const ZONAS = ['Europe/Madrid', 'America/New_York', 'America/Chicago', 'America/Los_Angeles', 'America/Mexico_City', 'Europe/London', 'Asia/Tokyo', 'UTC']

const AYUDA_CRON =
  'Una expresión cron de cinco campos por línea: minuto hora día-del-mes mes día-de-la-semana. Ejemplos: «0 10 * * 1-5» (laborables a las 10:00), «*/15 * * * *» (cada 15 minutos), «30 8 1 * *» (el día 1 de cada mes a las 8:30). Si escribes varias líneas, se lanza cuando cualquiera coincide.'

const fechaHora = (iso: string) => new Date(iso).toLocaleString('es-ES', { dateStyle: 'short', timeStyle: 'short' })

function aSolicitud(values: LanzadorFormValues): flujosApi.GuardarLanzadorDeCasoRequest {
  return {
    nombre: values.nombre.trim(),
    descripcion: values.descripcion.trim() || null,
    creadorDeCasoId: values.creadorDeCasoId,
    datosJson: values.datosJson.trim() || null,
    cron: values.cron.trim(),
    zonaHoraria: values.zonaHoraria,
    toleranciaMinutos: Number(values.toleranciaMinutos) || 120,
    activo: values.activo,
  }
}

/**
 * The launchers of a process: the crontab of the platform. A launcher is a creator that fires by itself — when its cron says, Viriato opens a case
 * with what the creator describes and the data written here (laid over the creator's defaults), with nobody pressing anything. It is how work that has to happen every day
 * (analyse the list at 10:00) starts, without a robot whose only job is to look at the clock.
 */
export function FlujoLanzadoresPanel({ flujoId }: { flujoId: string }) {
  const creadoresQuery = useQuery({ queryKey: ['flujo-creadores', flujoId], queryFn: () => flujosApi.listCreadoresDeCaso(flujoId) })
  const creadores = useMemo(() => (creadoresQuery.data ?? []).filter((c) => c.activo).sort((a, b) => a.orden - b.orden), [creadoresQuery.data])
  const formulariosQuery = useQuery({ queryKey: ['flujo-formularios', flujoId], queryFn: () => flujosApi.listFormularios(flujoId) })

  // What a launcher's data are written against: the form of the creator it fires, starting from that creator's defaults.
  const delCreador = useMemo(() => {
    const formularios = formulariosQuery.data ?? []
    return (creadorId: string) => {
      const creador = creadores.find((c) => c.id === creadorId)
      return {
        esquemaJson: formularios.find((f) => f.id === creador?.formularioId)?.esquemaJson ?? null,
        baseJson: creador?.datosPorDefectoJson ?? null,
      }
    }
  }, [creadores, formulariosQuery.data])

  const campos = useMemo<CrudField<LanzadorFormValues>[]>(
    () => [
      { name: 'nombre', label: 'Nombre', required: true, helpText: 'Para reconocerlo en la lista, p. ej. «Análisis de la mañana».' },
      { name: 'descripcion', label: 'Descripción' },
      {
        name: 'creadorDeCasoId',
        label: 'Creador que lanza',
        type: 'select',
        required: true,
        options: [{ value: '', label: 'Elige un creador…' }, ...creadores.map((c) => ({ value: c.id, label: c.nombre }))],
        helpText: 'El creador decide qué caso se abre: su tipo, el servicio por el que empieza, su estado y su título.',
      },
      { name: 'cron', label: 'Cuándo (cron)', type: 'textarea', mono: true, rows: 3, required: true, helpText: AYUDA_CRON },
      {
        name: 'zonaHoraria',
        label: 'Zona horaria',
        type: 'select',
        options: ZONAS.map((z) => ({ value: z, label: z })),
        helpText: 'Las horas del cron se leen en esta zona (el horario de un mercado es el suyo, no el del servidor).',
      },
      {
        name: 'datosJson',
        label: 'Datos del caso',
        type: 'custom',
        helpText:
          'Se rellenan con el formulario del creador y empiezan con sus valores por defecto: se guarda solo lo que cambies, así que un cambio en los valores del creador llega a todo lo que no hayas tocado. «Un caso por fila» abre un caso por cada fila en cada momento del cron.',
        validate: (valor, values) => {
          const { esquemaJson, baseJson } = delCreador(values.creadorDeCasoId)
          return erroresDeListaDeDatos(esquemaJson, baseJson, valor)[0] ?? null
        },
        render: ({ value, onChange, values, mostrarErrores }) => {
          const { esquemaJson, baseJson } = delCreador(values.creadorDeCasoId)
          return (
            <ListaDeDatosConFormulario
              key={values.creadorDeCasoId}
              esquemaJson={esquemaJson}
              baseJson={baseJson}
              value={value}
              onChange={onChange}
              mostrarErrores={mostrarErrores}
            />
          )
        },
      },
      {
        name: 'toleranciaMinutos',
        label: 'Tolerancia (minutos)',
        type: 'number',
        required: true,
        helpText: 'Hasta cuánto tarde puede lanzarse un momento. Si la plataforma estuvo parada más tiempo, ese momento se salta en vez de lanzarse con datos viejos.',
      },
      { name: 'activo', label: 'Activo (si no, está en pausa)', type: 'checkbox' },
    ],
    [creadores, delCreador],
  )

  const columns: CrudColumn<LanzadorDeCasoDto>[] = [
    {
      key: 'nombre',
      label: 'Lanzador',
      render: (l) => (
        <span className="flex flex-col">
          <span className="font-medium text-gray-900">{l.nombre}</span>
          {l.descripcion && <span className="text-xs text-gray-500">{l.descripcion}</span>}
        </span>
      ),
    },
    { key: 'creador', label: 'Lanza', render: (l) => l.creadorNombre },
    {
      key: 'cron',
      label: 'Cuándo',
      render: (l) => (
        <span className="flex flex-col">
          {l.cron.split('\n').map((linea) => (
            <span key={linea} className="font-mono text-xs text-gray-700">
              {linea}
            </span>
          ))}
          <span className="text-xs text-gray-500">{l.zonaHoraria}</span>
        </span>
      ),
    },
    {
      key: 'proximo',
      label: 'Próximo',
      render: (l) => (l.activo && l.proximoDisparoEn ? <span className="num text-gray-700">{fechaHora(l.proximoDisparoEn)}</span> : <span className="text-gray-400">—</span>),
    },
    {
      key: 'ultimo',
      label: 'Último',
      render: (l) => {
        if (l.ultimoError) {
          return (
            <span className="flex max-w-xs flex-col gap-0.5" title={l.ultimoError}>
              <Badge tone="danger">No pudo abrir el caso</Badge>
              <span className="line-clamp-2 text-xs text-red-600">{l.ultimoError}</span>
            </span>
          )
        }
        // Created, it is marked as having fired at that moment so that it does not fire what went by before: that is not a launch.
        if (!l.ultimoDisparoEn || !l.ultimoCasoId) return <span className="text-gray-400">Aún no ha lanzado</span>
        return (
          <span className="flex flex-col">
            <span className="num text-gray-700">{fechaHora(l.ultimoDisparoEn)}</span>
            {l.ultimoCasoId && (
              <Link to={`/casos/${l.ultimoCasoId}`} className="text-xs font-medium text-indigo-600 hover:text-indigo-700">
                Ver caso
              </Link>
            )}
          </span>
        )
      },
    },
    { key: 'activo', label: 'Estado', render: (l) => (l.activo ? <Badge tone="success">Activo</Badge> : <Badge tone="neutral">En pausa</Badge>) },
  ]

  const form: Config = {
    createFields: campos.filter((c) => c.name !== 'activo'),
    editFields: campos,
    emptyValues: { nombre: '', descripcion: '', creadorDeCasoId: '', datosJson: '', cron: '', zonaHoraria: 'Europe/Madrid', toleranciaMinutos: '120', activo: true },
    toEditValues: (l) => ({
      nombre: l.nombre,
      descripcion: l.descripcion ?? '',
      creadorDeCasoId: l.creadorDeCasoId,
      datosJson: l.datosJson ?? '',
      cron: l.cron,
      zonaHoraria: l.zonaHoraria,
      toleranciaMinutos: String(l.toleranciaMinutos),
      activo: l.activo,
    }),
    toCreateInput: aSolicitud,
    toUpdateInput: aSolicitud,
    modalSize: 'lg',
  }

  if (creadoresQuery.isLoading || formulariosQuery.isLoading) return <p className="text-gray-500">Cargando…</p>

  return (
    <div className="flex flex-col gap-3">
      {creadores.length === 0 && (
        <p className="rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800">
          Un lanzador dispara un creador de caso. Crea primero un creador en la pestaña «Creadores».
        </p>
      )}
      <CrudPage<LanzadorDeCasoDto, LanzadorFormValues, flujosApi.GuardarLanzadorDeCasoRequest, flujosApi.GuardarLanzadorDeCasoRequest>
        title="Lanzadores"
        resourceKey={`flujo-${flujoId}-lanzadores`}
        getId={(l) => l.id}
        columns={columns}
        form={form}
        filters={{ mode: 'general', placeholder: 'Nombre del lanzador…' }}
        entidad={{ singular: 'lanzador', plural: 'lanzadores' }}
        nombreDeFila={(l) => l.nombre}
        api={{
          list: async (filters) => {
            const term = filters.search?.toLowerCase() ?? ''
            const todos = await flujosApi.listLanzadoresDeCaso(flujoId)
            const filtrados = todos.filter((l) => !term || l.nombre.toLowerCase().includes(term))
            return { items: filtrados, page: 1, pageSize: filtrados.length || 1, total: filtrados.length }
          },
          create: (input) => flujosApi.createLanzadorDeCaso(flujoId, input),
          update: (id, input) => flujosApi.replaceLanzadorDeCaso(flujoId, id, input),
          remove: (id) => flujosApi.deleteLanzadorDeCaso(flujoId, id),
        }}
        deleteConfirm={{ message: '¿Seguro que quieres eliminar este lanzador? Los casos que ya abrió no cambian; solo deja de lanzar.' }}
      />
    </div>
  )
}
