import { useQuery, useQueryClient } from '@tanstack/react-query'
import { RotateCcw, Send } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { CrudPage } from '../../components/crud/CrudPage'
import type { CrudColumn, CrudField, CrudFormConfig } from '../../components/crud/types'
import { Badge } from '../../components/ui/Badge'
import { IconButton } from '../../components/ui/IconButton'
import { ApiError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import * as conexionesApi from '../conexiones/api'
import * as flujosApi from '../flujos/api'
import * as api from './api'
import type { Disparador, EnvioDeNotificacionDto, GuardarReglaRequest, ReglaDeNotificacionDto } from './api'

interface Valores {
  [key: string]: string | boolean
  nombre: string
  activa: boolean
  disparador: string
  evento: string
  estado: string
  tipoCasoId: string
  conexionId: string
  destinatarios: string
  asunto: string
  cuerpo: string
}

const DISPARADORES: { value: Disparador; label: string }[] = [
  { value: 'EventoDelRobot', label: 'El robot avisa de algo' },
  { value: 'EstadoNegocio', label: 'El caso llega a un estado de negocio' },
  { value: 'CasoFallido', label: 'El caso falla' },
  { value: 'CasoCompletado', label: 'El caso termina' },
]

const AYUDA_PLANTILLA =
  'Se puede usar {caso}, {proceso}, {tipo}, {estado}, {fecha}, {hora}, {enlace} (el caso en Viriato), {datos.campo} (los datos del caso) y {evento.campo} (lo que manda el robot al avisar).'

const fechaHora = (iso: string) => new Date(iso).toLocaleString('es-ES', { dateStyle: 'short', timeStyle: 'short' })

function aSolicitud(v: Valores): GuardarReglaRequest {
  const disparador = v.disparador as Disparador
  return {
    nombre: v.nombre.trim(),
    activa: v.activa,
    disparador,
    codigo: disparador === 'EventoDelRobot' ? v.evento.trim() : disparador === 'EstadoNegocio' ? v.estado : null,
    tipoCasoId: v.tipoCasoId || null,
    conexionId: v.conexionId,
    destinatarios: api.leerDestinatarios(v.destinatarios),
    asunto: v.asunto,
    cuerpo: v.cuerpo,
  }
}

const TONO_DE_ESTADO = { Pendiente: 'warning', Enviado: 'success', Fallido: 'danger' } as const

/**
 * The notices of a process: when something happens to one of its cases — a robot says so ("precio_bajado"), it reaches a business
 * state, it fails or ends — a message goes out through one of the platform's accounts (Configuración → Conexiones). Below, what
 * was sent, and a way to send again what could not be.
 */
export function FlujoNotificacionesPanel({ flujoId }: { flujoId: string }) {
  const { showToast } = useToast()
  const queryClient = useQueryClient()
  const [ocupado, setOcupado] = useState<string | null>(null)
  const tiposQuery = useQuery({ queryKey: ['flujo-tipos', flujoId], queryFn: () => flujosApi.listFlujoTiposCaso(flujoId) })
  const estadosQuery = useQuery({ queryKey: ['flujo-estados', flujoId], queryFn: () => flujosApi.listFlujoEstados(flujoId) })
  const conexionesQuery = useQuery({ queryKey: ['conexiones-para-enviar'], queryFn: conexionesApi.listConexionesParaEnviar })
  const conexiones = useMemo(() => conexionesQuery.data ?? [], [conexionesQuery.data])

  const campos = useMemo<CrudField<Valores>[]>(
    () => [
      { name: 'nombre', label: 'Nombre', required: true, helpText: 'Para reconocerla, p. ej. «Precio bajado».' },
      { name: 'disparador', label: 'Cuándo', type: 'select', required: true, options: DISPARADORES },
      {
        name: 'evento',
        label: 'Evento del robot',
        required: (v) => v.disparador === 'EventoDelRobot',
        visibleWhen: (v) => v.disparador === 'EventoDelRobot',
        validate: (valor) => (valor === '' || /^[a-z0-9_]{1,60}$/.test(valor.trim()) ? null : 'Letras minúsculas, números o _ (p. ej. precio_bajado).'),
        helpText: 'El código con el que avisa el robot, p. ej. precio_bajado.',
      },
      {
        name: 'estado',
        label: 'Estado de negocio',
        type: 'select',
        required: (v) => v.disparador === 'EstadoNegocio',
        visibleWhen: (v) => v.disparador === 'EstadoNegocio',
        options: [{ value: '', label: 'Elige un estado…' }, ...(estadosQuery.data ?? []).map((e) => ({ value: e.codigo, label: e.display }))],
      },
      {
        name: 'tipoCasoId',
        label: 'Tipo de caso',
        type: 'select',
        options: [{ value: '', label: 'Cualquiera' }, ...(tiposQuery.data ?? []).map((t) => ({ value: t.id, label: t.nombre }))],
      },
      {
        name: 'conexionId',
        label: 'Enviar por',
        type: 'select',
        required: true,
        options: [{ value: '', label: 'Elige una conexión…' }, ...conexiones.map((c) => ({ value: c.id, label: `${c.nombre} (${c.tipoNombre})` }))],
        helpText: 'Las cuentas se dan de alta en Configuración → Conexiones.',
      },
      { name: 'destinatarios', label: 'A quién', type: 'textarea', rows: 2, required: true, helpText: 'Direcciones de correo, una por línea o separadas por comas.' },
      { name: 'asunto', label: 'Asunto', required: true, helpText: AYUDA_PLANTILLA },
      { name: 'cuerpo', label: 'Texto', type: 'textarea', rows: 6, required: true },
      { name: 'activa', label: 'Activa', type: 'checkbox' },
    ],
    [conexiones, estadosQuery.data, tiposQuery.data],
  )

  const probar = async (regla: ReglaDeNotificacionDto) => {
    const destinatario = window.prompt(`¿A qué dirección mando la prueba de «${regla.nombre}»? (vacío: a sus destinatarios)`, '')
    if (destinatario === null) return
    setOcupado(regla.id)
    try {
      const r = await api.probarRegla(flujoId, regla.id, destinatario.trim() || null)
      if (r.enviado) showToast('success', `Prueba enviada a ${r.destinatarios.join(', ')}: «${r.asunto}».`)
      else showToast('error', r.error ?? 'No se pudo enviar la prueba.')
    } catch (error) {
      showToast('error', error instanceof ApiError ? error.message : 'No se pudo enviar la prueba.')
    } finally {
      setOcupado(null)
    }
  }

  const reintentar = async (envio: EnvioDeNotificacionDto) => {
    setOcupado(envio.id)
    try {
      await api.reintentarEnvio(flujoId, envio.id)
      showToast('info', 'Vuelve a la cola: sale en unos segundos.')
      await queryClient.invalidateQueries({ queryKey: [`flujo-${flujoId}-envios`] })
    } catch (error) {
      showToast('error', error instanceof ApiError ? error.message : 'No se pudo reintentar.')
    } finally {
      setOcupado(null)
    }
  }

  const columnasReglas: CrudColumn<ReglaDeNotificacionDto>[] = [
    { key: 'nombre', label: 'Regla', render: (r) => <span className="font-medium text-gray-900">{r.nombre}</span> },
    {
      key: 'cuando',
      label: 'Cuándo',
      render: (r) => (
        <span className="flex flex-col">
          <span className="text-sm text-gray-700">{DISPARADORES.find((d) => d.value === r.disparador)?.label}</span>
          {r.codigo && <span className="font-mono text-xs text-gray-500">{r.codigo}</span>}
          <span className="text-xs text-gray-500">{r.tipoCasoNombre ?? 'Cualquier tipo'}</span>
        </span>
      ),
    },
    {
      key: 'a',
      label: 'A quién',
      render: (r) => (
        <span className="flex flex-col text-sm text-gray-700">
          {r.destinatarios.join(', ')}
          <span className="text-xs text-gray-500">por {r.conexionNombre}</span>
        </span>
      ),
    },
    { key: 'activa', label: 'Estado', render: (r) => (r.activa ? <Badge tone="success">Activa</Badge> : <Badge tone="neutral">En pausa</Badge>) },
    {
      key: 'probar',
      label: '',
      render: (r) => (
        <IconButton size="sm" label={`Enviar una prueba de ${r.nombre}`} disabled={ocupado === r.id} onClick={() => void probar(r)}>
          <Send size={15} aria-hidden="true" />
        </IconButton>
      ),
    },
  ]

  const columnasEnvios: CrudColumn<EnvioDeNotificacionDto>[] = [
    { key: 'cuando', label: 'Cuándo', render: (e) => <span className="num text-sm text-gray-700">{fechaHora(e.createdAt)}</span> },
    {
      key: 'aviso',
      label: 'Aviso',
      render: (e) => (
        <span className="flex flex-col">
          <span className="text-sm text-gray-900">{e.asunto}</span>
          <span className="text-xs text-gray-500">
            {e.reglaNombre} ·{' '}
            <Link to={`/casos/${e.casoId}`} className="font-medium text-indigo-600 hover:text-indigo-700">
              {e.casoTitulo}
            </Link>
          </span>
        </span>
      ),
    },
    { key: 'a', label: 'A quién', render: (e) => <span className="text-sm text-gray-700">{e.destinatarios.join(', ')}</span> },
    {
      key: 'estado',
      label: 'Estado',
      render: (e) => (
        <span className="flex max-w-xs flex-col gap-0.5" title={e.ultimoError ?? undefined}>
          <Badge tone={TONO_DE_ESTADO[e.estado]}>{e.estado === 'Pendiente' && e.intentos > 0 ? `Reintentando (${e.intentos})` : e.estado}</Badge>
          {e.ultimoError && <span className="line-clamp-2 text-xs text-red-600">{e.ultimoError}</span>}
        </span>
      ),
    },
    {
      key: 'reintentar',
      label: '',
      render: (e) =>
        e.estado === 'Fallido' ? (
          <IconButton size="sm" label="Volver a intentarlo" disabled={ocupado === e.id} onClick={() => void reintentar(e)}>
            <RotateCcw size={15} aria-hidden="true" />
          </IconButton>
        ) : null,
    },
  ]

  const form: CrudFormConfig<ReglaDeNotificacionDto, Valores, GuardarReglaRequest, GuardarReglaRequest> = {
    createFields: campos,
    editFields: campos,
    emptyValues: {
      nombre: '',
      activa: true,
      disparador: 'EventoDelRobot',
      evento: '',
      estado: '',
      tipoCasoId: '',
      conexionId: conexiones[0]?.id ?? '',
      destinatarios: '',
      asunto: '',
      cuerpo: '',
    },
    toEditValues: (r) => ({
      nombre: r.nombre,
      activa: r.activa,
      disparador: r.disparador,
      evento: r.disparador === 'EventoDelRobot' ? (r.codigo ?? '') : '',
      estado: r.disparador === 'EstadoNegocio' ? (r.codigo ?? '') : '',
      tipoCasoId: r.tipoCasoId ?? '',
      conexionId: r.conexionId,
      destinatarios: r.destinatarios.join('\n'),
      asunto: r.asunto,
      cuerpo: r.cuerpo,
    }),
    toCreateInput: aSolicitud,
    toUpdateInput: aSolicitud,
  }

  if (conexionesQuery.isLoading) return <p className="text-gray-500">Cargando…</p>

  return (
    <div className="flex flex-col gap-6">
      {conexiones.length === 0 && (
        <p className="rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800">
          Para enviar avisos hace falta una conexión (un buzón de correo): créala en{' '}
          <Link to="/admin/conexiones" className="font-medium underline">
            Configuración → Conexiones
          </Link>
          .
        </p>
      )}
      <CrudPage<ReglaDeNotificacionDto, Valores, GuardarReglaRequest, GuardarReglaRequest>
        title="Notificaciones"
        resourceKey={`flujo-${flujoId}-notificaciones`}
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
        deleteConfirm={{ message: '¿Seguro que quieres eliminar esta regla? Lo que ya envió sigue en la lista de envíos.' }}
      />
      <CrudPage<EnvioDeNotificacionDto, Record<string, string | boolean>, never, never>
        title="Envíos"
        resourceKey={`flujo-${flujoId}-envios`}
        getId={(e) => e.id}
        columns={columnasEnvios}
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
                { value: 'Enviado', label: 'Enviados' },
                { value: 'Fallido', label: 'Fallidos' },
              ],
            },
          ],
        }}
        entidad={{ singular: 'envío', plural: 'envíos' }}
        api={{ list: (filtros) => api.listEnvios(flujoId, filtros) }}
      />
    </div>
  )
}
