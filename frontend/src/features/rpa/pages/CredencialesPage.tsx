import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { KeyRound, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Button } from '../../../components/ui/Button'
import { Badge } from '../../../components/ui/Badge'
import type { ColumnaDeTabla } from '../../../components/ui/DataTable'
import { IconButton } from '../../../components/ui/IconButton'
import { ListaDeDatos } from '../../../components/ui/ListaDeDatos'
import { PageHeader } from '../../../components/ui/PageHeader'
import { ConfirmDialog } from '../../../components/ui/ConfirmDialog'
import { Input } from '../../../components/ui/Input'
import { Modal } from '../../../components/ui/Modal'
import { ApiError } from '../../../lib/apiClient'
import { useToast } from '../../../lib/toast/useToast'
import { useListaPaginada } from '../../../lib/useListaPaginada'
import * as rpaApi from '../api'
import type { CredencialDto } from '../api'

interface FormState {
  nombre: string
  descripcion: string
  usuario: string
  password: string
  servicioId: string
  activo: boolean
}

const emptyForm: FormState = { nombre: '', descripcion: '', usuario: '', password: '', servicioId: '', activo: true }

// Same rule the API enforces: it is the key a robot uses to ask for the credential, so it stays URL-safe.
const NOMBRE_VALIDO = /^[a-z0-9][a-z0-9._-]*$/

const selectClass =
  'field'

export function CredencialesPage() {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [editing, setEditing] = useState<CredencialDto | 'nueva' | null>(null)
  const [form, setForm] = useState<FormState>(emptyForm)
  const [pendingDelete, setPendingDelete] = useState<CredencialDto | null>(null)

  const fuente = useListaPaginada<CredencialDto>({
    clave: ['credenciales'],
    obtenerId: (c) => c.id,
    cargar: ({ busqueda, pagina, tamano }) => rpaApi.listCredenciales({ search: busqueda, page: String(pagina), pageSize: String(tamano) }),
  })
  const serviciosQuery = useQuery({ queryKey: ['servicios-all'], queryFn: () => rpaApi.listServicios() })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['credenciales'] })

  const esNueva = editing === 'nueva'
  const credencialEnEdicion = editing !== null && editing !== 'nueva' ? editing : null

  const saveMutation = useMutation({
    mutationFn: () => {
      if (credencialEnEdicion) {
        return rpaApi.updateCredencial(credencialEnEdicion.id, {
          descripcion: form.descripcion.trim() || null,
          usuario: form.usuario.trim() || null,
          servicioId: form.servicioId || null,
          activo: form.activo,
          password: form.password || null,
        })
      }
      return rpaApi.createCredencial({
        nombre: form.nombre,
        descripcion: form.descripcion.trim() || null,
        usuario: form.usuario.trim() || null,
        password: form.password,
        servicioId: form.servicioId || null,
      })
    },
    onSuccess: () => {
      invalidate()
      showToast('success', esNueva ? 'Credencial creada.' : 'Credencial actualizada.')
      cerrar()
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo guardar la credencial.'),
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => rpaApi.deleteCredencial(id),
    onSuccess: () => {
      invalidate()
      setPendingDelete(null)
      showToast('success', 'Credencial eliminada.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo eliminar la credencial.'),
  })

  // An inactive servicio is not offered for new assignments, but a credential already tied to one keeps showing it.
  const servicios = (serviciosQuery.data?.items ?? []).filter((s) => s.activo || s.id === credencialEnEdicion?.servicioId)

  function abrirNueva() {
    setForm(emptyForm)
    setEditing('nueva')
  }

  function abrirEdicion(credencial: CredencialDto) {
    setForm({
      nombre: credencial.nombre,
      descripcion: credencial.descripcion ?? '',
      usuario: credencial.usuario ?? '',
      password: '',
      servicioId: credencial.servicioId ?? '',
      activo: credencial.activo,
    })
    setEditing(credencial)
  }

  function cerrar() {
    setEditing(null)
    setForm(emptyForm)
  }

  const set = <K extends keyof FormState>(campo: K, valor: FormState[K]) => setForm((prev) => ({ ...prev, [campo]: valor }))

  const columnas: ColumnaDeTabla<CredencialDto>[] = [
    { clave: 'nombre', titulo: 'Nombre', celda: (c) => <span className="font-mono text-xs text-gray-800">{c.nombre}</span> },
    { clave: 'descripcion', titulo: 'Descripción', celda: (c) => <span className="text-gray-600">{c.descripcion ?? '—'}</span> },
    { clave: 'usuario', titulo: 'Usuario', celda: (c) => c.usuario ?? '—' },
    { clave: 'password', titulo: 'Contraseña', celda: () => <span className="text-gray-400">••••••••</span> },
    { clave: 'servicio', titulo: 'Disponible para', celda: (c) => c.servicioNombre ?? 'Todos los robots' },
    { clave: 'activa', titulo: 'Activa', celda: (c) => <Badge tone={c.activo ? 'success' : 'neutral'}>{c.activo ? 'Activa' : 'Inactiva'}</Badge> },
    {
      clave: 'acceso',
      titulo: 'Último acceso',
      celda: (c) => <span className="text-gray-500">{c.ultimoAccesoAt ? new Date(c.ultimoAccesoAt).toLocaleString() : 'Nunca'}</span>,
    },
    {
      clave: 'acciones',
      titulo: 'Acciones',
      celda: (c) => (
        <div className="-my-1 flex items-center gap-0.5">
          <IconButton size="sm" label="Editar" onClick={() => abrirEdicion(c)}>
            <Pencil size={16} />
          </IconButton>
          <IconButton size="sm" variant="danger" label="Eliminar" onClick={() => setPendingDelete(c)}>
            <Trash2 size={16} />
          </IconButton>
        </div>
      ),
    },
  ]

  const nombreInvalido = esNueva && form.nombre !== '' && !NOMBRE_VALIDO.test(form.nombre)
  const puedeGuardar = esNueva ? NOMBRE_VALIDO.test(form.nombre) && form.password !== '' : true

  return (
    <div className="flex flex-col gap-4">
      <PageHeader
        title="Credenciales"
        description="Los robots piden aquí los usuarios y contraseñas que necesitan, por su nombre. Las contraseñas se guardan cifradas y no se pueden volver a ver: solo se pueden sustituir."
        actions={
          <Button onClick={abrirNueva}>
            <Plus size={16} />
            Nueva
          </Button>
        }
      />

      <ListaDeDatos
        fuente={fuente}
        columnas={columnas}
        obtenerId={(c) => c.id}
        nombreDeFila={(c) => c.nombre}
        entidad={{ singular: 'credencial', plural: 'credenciales' }}
        buscador={{ etiqueta: 'Buscar credenciales', placeholder: 'Buscar por nombre o usuario…' }}
        vacio={{
          icono: <KeyRound size={22} />,
          titulo: 'Sin credenciales',
          descripcion: 'Guarda aquí el usuario y la contraseña que necesita un robot, y pídela por su nombre desde el robot.',
          accion: (
            <Button variant="secondary" size="sm" onClick={abrirNueva}>
              <Plus size={15} />
              Nueva
            </Button>
          ),
        }}
        anchoMinimo="900px"
      />

      <Modal
        open={editing !== null}
        title={esNueva ? 'Nueva credencial' : 'Editar credencial'}
        onClose={cerrar}
        footer={
          <div className="flex justify-end gap-2">
            <Button type="button" variant="ghost" onClick={cerrar}>
              Cancelar
            </Button>
            <Button type="button" disabled={!puedeGuardar || saveMutation.isPending} onClick={() => saveMutation.mutate()}>
              {saveMutation.isPending ? 'Guardando…' : 'Guardar'}
            </Button>
          </div>
        }
      >
        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-1">
            <Input
              label="Nombre"
              name="nombre"
              value={form.nombre}
              disabled={!esNueva}
              autoComplete="off"
              onChange={(e) => set('nombre', e.target.value)}
              error={nombreInvalido ? 'Solo minúsculas, números, punto, guion y guion bajo.' : undefined}
            />
            <p className="text-xs text-gray-500">
              Es lo que el robot usa para pedirla, p. ej. <code>tradeplace</code>.
              {esNueva ? ' No se podrá cambiar después.' : ' No se puede cambiar.'}
            </p>
          </div>
          <Input label="Descripción" name="descripcion" value={form.descripcion} onChange={(e) => set('descripcion', e.target.value)} />
          <Input label="Usuario" name="usuario" value={form.usuario} autoComplete="off" onChange={(e) => set('usuario', e.target.value)} />
          <div className="flex flex-col gap-1">
            <Input
              label="Contraseña"
              name="password"
              type="password"
              value={form.password}
              autoComplete="new-password"
              placeholder={esNueva ? '' : 'Déjala en blanco para no cambiarla'}
              onChange={(e) => set('password', e.target.value)}
            />
            {!esNueva && <p className="text-xs text-gray-500">Hay una contraseña guardada. Escribe una nueva solo si quieres sustituirla.</p>}
          </div>
          <div className="flex flex-col gap-1">
            <label htmlFor="servicio" className="text-sm font-medium text-gray-700">
              Disponible para
            </label>
            <select id="servicio" className={selectClass} value={form.servicioId} onChange={(e) => set('servicioId', e.target.value)}>
              <option value="">Todos los robots</option>
              {servicios.map((servicio) => (
                <option key={servicio.id} value={servicio.id}>
                  Solo el servicio «{servicio.nombre}»
                </option>
              ))}
            </select>
          </div>
          {!esNueva && (
            <label className="flex items-center gap-2 text-sm text-gray-700">
              <input type="checkbox" className="accent-indigo-600" checked={form.activo} onChange={(e) => set('activo', e.target.checked)} />
              Activa (si no, los robots no pueden leerla)
            </label>
          )}
        </div>
      </Modal>

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Eliminar credencial"
        message="¿Seguro que quieres eliminar esta credencial? Los robots que la usen dejarán de poder leerla y fallarán hasta que la vuelvas a crear."
        pending={deleteMutation.isPending}
        onConfirm={() => pendingDelete && deleteMutation.mutate(pendingDelete.id)}
        onCancel={() => setPendingDelete(null)}
      />
    </div>
  )
}
