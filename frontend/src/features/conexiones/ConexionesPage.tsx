import { useQuery } from '@tanstack/react-query'
import { Send } from 'lucide-react'
import { useMemo, useState } from 'react'
import { CrudPage } from '../../components/crud/CrudPage'
import type { CrudColumn, CrudField, CrudFormConfig } from '../../components/crud/types'
import { Badge } from '../../components/ui/Badge'
import { IconButton } from '../../components/ui/IconButton'
import { ApiError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import * as conexionesApi from './api'
import type { ConexionDto, GuardarConexionRequest, TipoDeConexionDto } from './api'

type Valores = Record<string, string | boolean>

/** The form field of one setting of one kind: every kind's fields live in the same form, each shown only while its kind is chosen. */
const campoDe = (tipo: string, campo: string) => `cfg__${tipo}__${campo}`

function aSolicitud(valores: Valores, tipos: TipoDeConexionDto[]): GuardarConexionRequest {
  const tipo = tipos.find((t) => t.codigo === valores.tipo)
  const configuracion: Record<string, string | number | boolean> = {}
  for (const campo of tipo?.campos ?? []) {
    const valor = valores[campoDe(tipo!.codigo, campo.codigo)]
    if (campo.tipo === 'booleano') configuracion[campo.codigo] = Boolean(valor)
    else if (typeof valor === 'string' && valor.trim() !== '') configuracion[campo.codigo] = campo.tipo === 'numero' ? Number(valor) : valor.trim()
  }
  return {
    nombre: String(valores.nombre).trim(),
    tipo: String(valores.tipo),
    configuracion,
    secreto: typeof valores.secreto === 'string' && valores.secreto !== '' ? valores.secreto : null,
    activa: Boolean(valores.activa),
  }
}

/**
 * The platform's accounts with the outside world — a mailbox today, a Telegram bot or an API tomorrow. Configured once here, used by
 * name by whatever needs them: the notification rules of the processes first. The form draws each kind's settings from what the server
 * says the kind needs, so a new kind needs no change here. The password is never shown again once saved.
 */
export function ConexionesPage() {
  const { showToast } = useToast()
  const [probando, setProbando] = useState<string | null>(null)
  const tiposQuery = useQuery({ queryKey: ['conexiones-tipos'], queryFn: conexionesApi.listTiposDeConexion })
  const tipos = useMemo(() => tiposQuery.data ?? [], [tiposQuery.data])

  const campos = useMemo<CrudField<Valores>[]>(
    () => [
      { name: 'nombre', label: 'Nombre', required: true, helpText: 'Para elegirla desde otros sitios, p. ej. «Correo de avisos».' },
      { name: 'tipo', label: 'Tipo', type: 'select', required: true, options: tipos.map((t) => ({ value: t.codigo, label: t.nombre })) },
      ...tipos.flatMap((t) =>
        t.campos.map<CrudField<Valores>>((c) => ({
          name: campoDe(t.codigo, c.codigo),
          label: c.etiqueta,
          type: c.tipo === 'booleano' ? 'checkbox' : c.tipo === 'numero' ? 'number' : 'text',
          required: (v) => c.obligatorio && v.tipo === t.codigo,
          helpText: c.ayuda ?? undefined,
          visibleWhen: (v) => v.tipo === t.codigo,
        })),
      ),
      {
        name: 'secreto',
        label: 'Contraseña',
        type: 'password',
        helpText: 'Se guarda cifrada y no se vuelve a mostrar. Al editar, déjala vacía para conservar la que hay.',
        visibleWhen: (v) => Boolean(tipos.find((t) => t.codigo === v.tipo)?.etiquetaSecreto),
      },
      { name: 'activa', label: 'Activa (si no, nada sale por ella)', type: 'checkbox' },
    ],
    [tipos],
  )

  const vacios = useMemo<Valores>(() => {
    const valores: Valores = { nombre: '', tipo: tipos[0]?.codigo ?? '', secreto: '', activa: true }
    for (const t of tipos) for (const c of t.campos) valores[campoDe(t.codigo, c.codigo)] = c.tipo === 'booleano' ? Boolean(c.porDefecto) : String(c.porDefecto ?? '')
    return valores
  }, [tipos])

  const probar = async (conexion: ConexionDto) => {
    const destinatario = window.prompt(`¿A qué dirección mando la prueba de «${conexion.nombre}»? (vacío: al remitente)`, '')
    if (destinatario === null) return
    setProbando(conexion.id)
    try {
      const resultado = await conexionesApi.probarConexion(conexion.id, destinatario.trim() || null)
      if (resultado.enviado) showToast('success', `Prueba enviada a ${resultado.destinatario}.`)
      else showToast('error', resultado.error ?? 'No se pudo enviar la prueba.')
    } catch (error) {
      showToast('error', error instanceof ApiError ? error.message : 'No se pudo enviar la prueba.')
    } finally {
      setProbando(null)
    }
  }

  const columnas: CrudColumn<ConexionDto>[] = [
    { key: 'nombre', label: 'Conexión', render: (c) => <span className="font-medium text-gray-900">{c.nombre}</span> },
    { key: 'tipo', label: 'Tipo', render: (c) => c.tipoNombre },
    {
      key: 'detalle',
      label: 'Cuenta',
      render: (c) => <span className="text-sm text-gray-600">{String(c.configuracion.remitente ?? c.configuracion.usuario ?? '—')}</span>,
    },
    { key: 'secreto', label: 'Contraseña', render: (c) => (c.tieneSecreto ? <Badge tone="success">Guardada</Badge> : <Badge tone="neutral">Sin contraseña</Badge>) },
    { key: 'activa', label: 'Estado', render: (c) => (c.activa ? <Badge tone="success">Activa</Badge> : <Badge tone="neutral">Desactivada</Badge>) },
    {
      key: 'probar',
      label: '',
      render: (c) =>
        c.enviaMensajes ? (
          <IconButton size="sm" label={`Enviar una prueba por ${c.nombre}`} disabled={probando === c.id} onClick={() => void probar(c)}>
            <Send size={15} aria-hidden="true" />
          </IconButton>
        ) : null,
    },
  ]

  const form: CrudFormConfig<ConexionDto, Valores, GuardarConexionRequest, GuardarConexionRequest> = {
    createFields: campos,
    editFields: campos.map((c) => (c.name === 'tipo' ? { ...c, disabled: true, helpText: 'Una conexión no cambia de tipo: crea otra.' } : c)),
    emptyValues: vacios,
    toEditValues: (c) => {
      const valores: Valores = { ...vacios, nombre: c.nombre, tipo: c.tipo, secreto: '', activa: c.activa }
      for (const [campo, valor] of Object.entries(c.configuracion)) valores[campoDe(c.tipo, campo)] = typeof valor === 'boolean' ? valor : String(valor)
      return valores
    },
    toCreateInput: (v) => aSolicitud(v, tipos),
    toUpdateInput: (v) => aSolicitud(v, tipos),
  }

  if (tiposQuery.isLoading) return <p className="text-gray-500">Cargando…</p>

  return (
    <CrudPage<ConexionDto, Valores, GuardarConexionRequest, GuardarConexionRequest>
      title="Conexiones"
      resourceKey="conexiones"
      getId={(c) => c.id}
      columns={columnas}
      form={form}
      filters={{ mode: 'general', placeholder: 'Nombre de la conexión…' }}
      entidad={{ singular: 'conexión', plural: 'conexiones' }}
      nombreDeFila={(c) => c.nombre}
      api={{
        list: async (filters) => {
          const term = filters.search?.toLowerCase() ?? ''
          const todas = (await conexionesApi.listConexiones()).filter((c) => !term || c.nombre.toLowerCase().includes(term))
          return { items: todas, page: 1, pageSize: todas.length || 1, total: todas.length }
        },
        create: conexionesApi.createConexion,
        update: conexionesApi.updateConexion,
        remove: conexionesApi.deleteConexion,
      }}
      deleteConfirm={{ message: '¿Seguro que quieres eliminar esta conexión? Si alguna regla de notificación la usa, no se podrá.' }}
    />
  )
}
