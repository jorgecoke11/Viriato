import { useQuery } from '@tanstack/react-query'
import { useMemo } from 'react'
import { Link } from 'react-router-dom'
import { CrudPage } from '../../components/crud/CrudPage'
import type { CrudColumn, CrudField, CrudFormConfig } from '../../components/crud/types'
import { Badge } from '../../components/ui/Badge'
import * as flujosApi from '../flujos/api'
import * as api from './api'
import type { GuardarReglaDeReprocesamientoRequest, ReglaDeReprocesamientoDto, ReprocesamientoProgramadoDto, ValoresDeRegla } from './api'

const fechaHora = (iso: string) => new Date(iso).toLocaleString('es-ES', { dateStyle: 'short', timeStyle: 'short' })

const TONO_DE_ESTADO = { Pendiente: 'warning', Hecho: 'success', Fallido: 'danger', Descartado: 'neutral' } as const

/**
 * The reprocessing rules of a process: when one of its cases ends in a business state and/or with its last execution in some state,
 * a step runs again — right away or after a wait — and, with a limit, once it has been tried that many times the case is left on a final
 * business state. Below, what the rules decided.
 */
export function FlujoReprocesamientosPanel({ flujoId }: { flujoId: string }) {
  const estadosQuery = useQuery({ queryKey: ['flujo-estados', flujoId], queryFn: () => flujosApi.listFlujoEstados(flujoId) })
  const pasosQuery = useQuery({ queryKey: ['flujo-reprocesamientos-pasos', flujoId], queryFn: () => api.listPasos(flujoId) })

  const campos = useMemo<CrudField<ValoresDeRegla>[]>(() => {
    const estados = (estadosQuery.data ?? []).map((e) => ({ value: e.id, label: e.display }))
    return [
      { name: 'nombre', label: 'Nombre', required: true, helpText: 'Para reconocerla, p. ej. «Reintentar si el portal falla».' },
      {
        name: 'estadoNegocioId',
        label: 'Si el caso acaba en el estado de negocio',
        type: 'select',
        options: [{ value: '', label: 'Cualquiera' }, ...estados],
      },
      {
        name: 'estadoEjecucion',
        label: 'Y su última ejecución acabó',
        type: 'select',
        // Without a business state, the execution's state is what the rule is about.
        required: (v) => v.estadoNegocioId === '',
        options: [{ value: '', label: 'De cualquier forma' }, ...api.ESTADOS_DE_EJECUCION],
        helpText: 'Hace falta al menos uno de los dos. Un caso que una persona descarta no se reprocesa nunca.',
      },
      {
        name: 'pasoNombre',
        label: 'Qué se reprocesa',
        type: 'select',
        options: [{ value: '', label: 'La ejecución en que acabó el caso' }, ...(pasosQuery.data ?? []).map((p) => ({ value: p, label: p }))],
        helpText: 'Si es un paso anterior, después vuelven a correr los que le siguen.',
      },
      {
        name: 'esperaMinutos',
        label: 'Esperar (minutos)',
        type: 'number',
        required: true,
        validate: (valor) => (/^\d+$/.test(valor) && Number(valor) <= 10080 ? null : 'De 0 a 10080 minutos (una semana).'),
        helpText: '0: al momento.',
      },
      { name: 'conLimite', label: 'Poner límite', type: 'checkbox' },
      {
        name: 'limite',
        label: 'Reprocesar como mucho',
        type: 'number',
        required: (v) => v.conLimite,
        visibleWhen: (v) => v.conLimite,
        validate: (valor) => (/^\d+$/.test(valor) && Number(valor) >= 1 && Number(valor) <= 100 ? null : 'De 1 a 100 veces.'),
        helpText: 'Veces por caso. Cuando se agotan, el caso ya no se reprocesa más.',
      },
      {
        name: 'estadoFinalId',
        label: 'Al agotarse, dejar el caso en',
        type: 'select',
        required: (v) => v.conLimite,
        visibleWhen: (v) => v.conLimite,
        options: [{ value: '', label: 'Elige un estado…' }, ...estados],
      },
      {
        name: 'orden',
        label: 'Orden',
        type: 'number',
        helpText: 'Si varias reglas encajan con el mismo caso, solo se aplica la de menor orden.',
      },
      { name: 'activa', label: 'Activa', type: 'checkbox' },
    ]
  }, [estadosQuery.data, pasosQuery.data])

  const columnasReglas: CrudColumn<ReglaDeReprocesamientoDto>[] = [
    { key: 'nombre', label: 'Regla', render: (r) => <span className="font-medium text-gray-900">{r.nombre}</span> },
    { key: 'cuando', label: 'Cuándo', render: (r) => <span className="text-sm text-gray-700">{api.describirCuando(r)}</span> },
    {
      key: 'que',
      label: 'Qué se reprocesa',
      render: (r) => (
        <span className="flex flex-col">
          <span className="text-sm text-gray-700">{r.pasoNombre ?? 'La ejecución en que acabó'}</span>
          <span className="text-xs text-gray-500">{api.describirEspera(r.esperaMinutos)}</span>
        </span>
      ),
    },
    {
      key: 'limite',
      label: 'Límite',
      render: (r) =>
        r.limite === null ? (
          <span className="text-sm text-gray-500">Sin límite</span>
        ) : (
          <span className="flex flex-col">
            <span className="num text-sm text-gray-700">{r.limite} {r.limite === 1 ? 'vez' : 'veces'}</span>
            <span className="text-xs text-gray-500">luego «{r.estadoFinalNombre}»</span>
          </span>
        ),
    },
    { key: 'orden', label: 'Orden', render: (r) => <span className="num text-sm text-gray-700">{r.orden}</span> },
    { key: 'activa', label: 'Estado', render: (r) => (r.activa ? <Badge tone="success">Activa</Badge> : <Badge tone="neutral">En pausa</Badge>) },
  ]

  const columnasHistorial: CrudColumn<ReprocesamientoProgramadoDto>[] = [
    { key: 'cuando', label: 'Para', render: (p) => <span className="num text-sm text-gray-700">{fechaHora(p.programadoPara)}</span> },
    {
      key: 'caso',
      label: 'Caso',
      render: (p) => (
        <span className="flex flex-col">
          <Link to={`/casos/${p.casoId}`} className="text-sm font-medium text-indigo-600 hover:text-indigo-700">
            {p.casoTitulo}
          </Link>
          <span className="text-xs text-gray-500">
            {p.reglaNombre} · {p.pasoNombre ?? 'la ejecución en que acabó'} · nº {p.numero}
          </span>
        </span>
      ),
    },
    {
      key: 'estado',
      label: 'Estado',
      render: (p) => (
        <span className="flex max-w-xs flex-col items-start gap-0.5" title={p.error ?? undefined}>
          <Badge tone={TONO_DE_ESTADO[p.estado]}>{p.estado}</Badge>
          {p.error && <span className="line-clamp-2 text-xs text-gray-500">{p.error}</span>}
        </span>
      ),
    },
  ]

  const form: CrudFormConfig<ReglaDeReprocesamientoDto, ValoresDeRegla, GuardarReglaDeReprocesamientoRequest, GuardarReglaDeReprocesamientoRequest> = {
    createFields: campos,
    editFields: campos,
    emptyValues: api.VALORES_VACIOS,
    toEditValues: api.aValores,
    toCreateInput: api.aSolicitud,
    toUpdateInput: api.aSolicitud,
  }

  return (
    <div className="flex flex-col gap-6">
      <CrudPage<ReglaDeReprocesamientoDto, ValoresDeRegla, GuardarReglaDeReprocesamientoRequest, GuardarReglaDeReprocesamientoRequest>
        title="Reprocesamientos"
        resourceKey={`flujo-${flujoId}-reprocesamientos`}
        getId={(r) => r.id}
        columns={columnasReglas}
        form={form}
        filters={{ mode: 'none' }}
        entidad={{ singular: 'regla', plural: 'reglas' }}
        nombreDeFila={(r) => r.nombre}
        api={{
          list: async () => {
            const reglas = await api.listReglas(flujoId)
            return { items: reglas, page: 1, pageSize: reglas.length || 1, total: reglas.length }
          },
          create: (input) => api.createRegla(flujoId, input),
          update: (id, input) => api.updateRegla(flujoId, id, input),
          remove: (id) => api.deleteRegla(flujoId, id),
        }}
        deleteConfirm={{ message: '¿Seguro que quieres eliminar esta regla? Los reprocesamientos que ya decidió siguen en la lista.' }}
      />
      <CrudPage<ReprocesamientoProgramadoDto, Record<string, string | boolean>, never, never>
        title="Reprocesamientos decididos"
        resourceKey={`flujo-${flujoId}-reprocesamientos-historial`}
        getId={(p) => p.id}
        columns={columnasHistorial}
        filters={{
          mode: 'fields',
          fields: [
            {
              key: 'estado',
              label: 'Estado',
              type: 'select',
              options: [
                { value: '', label: 'Todos' },
                { value: 'Pendiente', label: 'Pendientes' },
                { value: 'Hecho', label: 'Hechos' },
                { value: 'Fallido', label: 'Fallidos' },
                { value: 'Descartado', label: 'Descartados' },
              ],
            },
          ],
        }}
        entidad={{ singular: 'reprocesamiento', plural: 'reprocesamientos' }}
        api={{ list: (filtros) => api.listHistorial(flujoId, filtros) }}
      />
    </div>
  )
}
