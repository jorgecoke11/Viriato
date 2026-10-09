import { useQuery } from '@tanstack/react-query'
import { useMemo } from 'react'
import { CrudPage } from '../../components/crud/CrudPage'
import type { CrudColumn, CrudField, CrudFormConfig } from '../../components/crud/types'
import { Badge } from '../../components/ui/Badge'
import * as casosApi from '../casos/api'
import * as flujosApi from './api'
import type { CreadorDeCasoDto } from './api'

interface CreadorFormValues {
  [key: string]: string | boolean
  nombre: string
  descripcion: string
  tipoCasoId: string
  pasoInicialNombre: string
  estadoNegocioInicialId: string
  plantillaTitulo: string
  orden: string
  activo: boolean
}

type Config = CrudFormConfig<CreadorDeCasoDto, CreadorFormValues, flujosApi.GuardarCreadorDeCasoRequest, flujosApi.GuardarCreadorDeCasoRequest>

const AYUDA_TITULO =
  'Cómo se escribe el título cuando el usuario no pone uno. Puedes usar {creador}, {proceso}, {tipo}, {fecha}, {hora}, {n} (número correlativo del creador) y {datos.campo} para un campo de los datos del caso.'

function aSolicitud(values: CreadorFormValues): flujosApi.GuardarCreadorDeCasoRequest {
  return {
    nombre: values.nombre.trim(),
    descripcion: values.descripcion.trim() || null,
    tipoCasoId: values.tipoCasoId || null,
    pasoInicialNombre: values.pasoInicialNombre || null,
    estadoNegocioInicialId: values.estadoNegocioInicialId || null,
    plantillaTitulo: values.plantillaTitulo.trim() || null,
    orden: Number(values.orden) || 1,
    activo: values.activo,
  }
}

/**
 * The creators of a process: ready-made ways of starting a case. Whoever manages the process decides here what each one
 * starts — the type (and with it the form of the data), the service it begins at, the business estado it is born in and how
 * its title is written — so that the person creating a case only picks one and fills in its data.
 */
export function FlujoCreadoresPanel({ flujoId, versionActivaId }: { flujoId: string; versionActivaId: string | null }) {
  const tiposQuery = useQuery({ queryKey: ['flujo-tipos-caso', flujoId], queryFn: () => flujosApi.listFlujoTiposCaso(flujoId) })
  const estadosQuery = useQuery({ queryKey: ['flujo-estados', flujoId], queryFn: () => flujosApi.listFlujoEstados(flujoId) })
  const versionQuery = useQuery({
    queryKey: ['flujo-version-activa', versionActivaId],
    queryFn: () => casosApi.getFlujoVersion(flujoId, versionActivaId!),
    enabled: Boolean(versionActivaId),
  })

  const tipos = useMemo(() => (tiposQuery.data ?? []).filter((t) => t.activo).sort((a, b) => a.orden - b.orden), [tiposQuery.data])
  const estados = useMemo(() => (estadosQuery.data ?? []).filter((e) => e.activo).sort((a, b) => a.orden - b.orden), [estadosQuery.data])
  // Any step can be where a case starts, but only the ones a service runs are a real choice for a person; the rest is plumbing.
  const pasos = useMemo(
    () => (versionQuery.data?.pasos ?? []).filter((p) => p.tipoPaso === 'Rpa').sort((a, b) => a.orden - b.orden),
    [versionQuery.data],
  )

  const campos = useMemo<CrudField<CreadorFormValues>[]>(
    () => [
      { name: 'nombre', label: 'Nombre', required: true, helpText: 'Lo que ve el usuario al elegir, p. ej. «Alta de cliente».' },
      { name: 'descripcion', label: 'Descripción', helpText: 'Una línea para distinguirlo de los demás.' },
      {
        name: 'tipoCasoId',
        label: 'Tipo de caso',
        type: 'select',
        options: [{ value: '', label: 'Sin tipo (datos en JSON libre)' }, ...tipos.map((t) => ({ value: t.id, label: t.nombre }))],
        helpText: 'El tipo marca el formulario de datos que verá el usuario.',
      },
      {
        name: 'pasoInicialNombre',
        label: 'Servicio por el que empieza',
        type: 'select',
        options: [
          { value: '', label: 'El primer paso del proceso' },
          ...pasos.map((p) => ({ value: p.nombre, label: p.nombre })),
        ],
        helpText: 'Los pasos anteriores quedan como omitidos. Se elige por nombre para que siga valiendo al publicar versiones nuevas.',
      },
      {
        name: 'estadoNegocioInicialId',
        label: 'Estado de negocio inicial',
        type: 'select',
        options: [{ value: '', label: 'Sin estado' }, ...estados.map((e) => ({ value: e.id, label: e.display }))],
      },
      { name: 'plantillaTitulo', label: 'Título automático', helpText: AYUDA_TITULO },
      { name: 'orden', label: 'Orden', type: 'number', required: true },
      { name: 'activo', label: 'Activo (se ofrece al crear casos)', type: 'checkbox' },
    ],
    [tipos, estados, pasos],
  )

  const columns: CrudColumn<CreadorDeCasoDto>[] = [
    {
      key: 'nombre',
      label: 'Creador',
      render: (c) => (
        <span className="flex flex-col">
          <span className="font-medium text-gray-900">{c.nombre}</span>
          {c.descripcion && <span className="text-xs text-gray-500">{c.descripcion}</span>}
        </span>
      ),
    },
    { key: 'tipo', label: 'Tipo de caso', render: (c) => c.tipoCasoNombre ?? <span className="text-gray-400">Sin tipo</span> },
    { key: 'paso', label: 'Empieza en', render: (c) => c.pasoInicialNombre ?? <span className="text-gray-400">Primer paso</span> },
    { key: 'estado', label: 'Estado inicial', render: (c) => c.estadoNegocioInicialDisplay ?? <span className="text-gray-400">—</span> },
    { key: 'titulo', label: 'Título', render: (c) => <span className="font-mono text-xs text-gray-600">{c.plantillaTitulo}</span> },
    { key: 'activo', label: 'Estado', render: (c) => (c.activo ? <Badge tone="success">Activo</Badge> : <Badge tone="neutral">Inactivo</Badge>) },
  ]

  const form: Config = {
    createFields: campos.filter((c) => c.name !== 'activo'),
    editFields: campos,
    emptyValues: {
      nombre: '', descripcion: '', tipoCasoId: '', pasoInicialNombre: '', estadoNegocioInicialId: '', plantillaTitulo: '', orden: '1', activo: true,
    },
    toEditValues: (c) => ({
      nombre: c.nombre,
      descripcion: c.descripcion ?? '',
      tipoCasoId: c.tipoCasoId ?? '',
      pasoInicialNombre: c.pasoInicialNombre ?? '',
      estadoNegocioInicialId: c.estadoNegocioInicialId ?? '',
      plantillaTitulo: c.plantillaTitulo,
      orden: String(c.orden),
      activo: c.activo,
    }),
    toCreateInput: aSolicitud,
    toUpdateInput: aSolicitud,
  }

  // The selects are built from what these queries return, so the form waits for them rather than offering empty lists.
  if (tiposQuery.isLoading || estadosQuery.isLoading || versionQuery.isLoading) return <p className="text-gray-500">Cargando…</p>

  return (
    <div className="flex flex-col gap-3">
      {!versionActivaId && (
        <p className="rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800">
          El proceso aún no tiene una versión publicada: los creadores no se ofrecerán hasta que la tenga.
        </p>
      )}
      <CrudPage<CreadorDeCasoDto, CreadorFormValues, flujosApi.GuardarCreadorDeCasoRequest, flujosApi.GuardarCreadorDeCasoRequest>
        title="Creadores de caso"
        resourceKey={`flujo-${flujoId}-creadores`}
        getId={(c) => c.id}
        columns={columns}
        form={form}
        filters={{ mode: 'general', placeholder: 'Nombre del creador…' }}
        entidad={{ singular: 'creador', plural: 'creadores' }}
        nombreDeFila={(c) => c.nombre}
        api={{
          list: async (filters) => {
            const term = filters.search?.toLowerCase() ?? ''
            const todos = await flujosApi.listCreadoresDeCaso(flujoId)
            const filtrados = todos.filter((c) => !term || c.nombre.toLowerCase().includes(term))
            return { items: filtrados, page: 1, pageSize: filtrados.length || 1, total: filtrados.length }
          },
          create: (input) => flujosApi.createCreadorDeCaso(flujoId, input),
          update: (id, input) => flujosApi.replaceCreadorDeCaso(flujoId, id, input),
          remove: (id) => flujosApi.deleteCreadorDeCaso(flujoId, id),
        }}
        deleteConfirm={{
          message: '¿Seguro que quieres eliminar este creador? Los casos ya creados con él no cambian; solo deja de ofrecerse.',
        }}
      />
    </div>
  )
}
