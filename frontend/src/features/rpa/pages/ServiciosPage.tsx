import { CrudPage } from '../../../components/crud/CrudPage'
import type { CrudColumn, CrudFormConfig } from '../../../components/crud/types'
import * as rpaApi from '../api'
import type { ServicioDto } from '../api'
import { ActivoBadge } from '../../../components/ui/ActivoBadge'

interface ServicioFormValues {
  [key: string]: string | boolean
  nombre: string
  descripcion: string
  activo: boolean
  tiempoMaximoMinutos: string
  maxEjecucionesGlobales: string
}

// An empty box means "no limit"; anything else was already checked as a whole number by the field's validation.
const aNumero = (texto: string): number | null => (texto.trim() === '' ? null : Number(texto))

const validarEntero = (maximo: number, queEs: string) => (valor: string): string | null => {
  if (valor.trim() === '') return null
  const n = Number(valor)
  return Number.isInteger(n) && n >= 1 && n <= maximo ? null : `${queEs} debe ser un número entero entre 1 y ${maximo}.`
}

const camposDeLimites: CrudFormConfig<ServicioDto, ServicioFormValues, rpaApi.CreateServicioInput, rpaApi.UpdateServicioInput>['createFields'] = [
  {
    name: 'tiempoMaximoMinutos',
    label: 'Tiempo máximo de ejecución (minutos)',
    type: 'number',
    helpText:
      'Si un paso de este servicio lleva en ejecución más que esto, se cancela el caso. Pon el doble de lo que tarda normalmente. Vacío = sin límite: un robot caído bloquearía su máquina para siempre.',
    validate: validarEntero(10_080, 'El tiempo máximo'),
  },
  {
    name: 'maxEjecucionesGlobales',
    label: 'Ejecuciones simultáneas en todas las máquinas',
    type: 'number',
    helpText: 'Para lo que se comparte entre máquinas, como una cuenta de un portal que no admite dos sesiones. Vacío = sin límite.',
    validate: validarEntero(1000, 'El límite'),
  },
]

const columns: CrudColumn<ServicioDto>[] = [
  { key: 'nombre', label: 'Nombre', render: (s) => s.nombre },
  { key: 'descripcion', label: 'Descripción', render: (s) => s.descripcion ?? '—' },
  {
    key: 'tiempoMaximoMinutos',
    label: 'Tiempo máximo',
    render: (s) => (s.tiempoMaximoMinutos === null ? <span className="text-gray-500">Sin límite</span> : `${s.tiempoMaximoMinutos} min`),
  },
  { key: 'activo', label: 'Activo', render: (s) => <ActivoBadge activo={s.activo} /> },
]

const createFields: CrudFormConfig<ServicioDto, ServicioFormValues, rpaApi.CreateServicioInput, rpaApi.UpdateServicioInput>['createFields'] = [
  { name: 'nombre', label: 'Nombre', required: true },
  { name: 'descripcion', label: 'Descripción', type: 'textarea' },
  ...camposDeLimites,
]

const editFields: CrudFormConfig<ServicioDto, ServicioFormValues, rpaApi.CreateServicioInput, rpaApi.UpdateServicioInput>['editFields'] = [
  { name: 'nombre', label: 'Nombre', required: true },
  { name: 'descripcion', label: 'Descripción', type: 'textarea' },
  { name: 'activo', label: 'Activo', type: 'checkbox' },
  ...camposDeLimites,
]

const form: CrudFormConfig<ServicioDto, ServicioFormValues, rpaApi.CreateServicioInput, rpaApi.UpdateServicioInput> = {
  createFields,
  editFields,
  emptyValues: { nombre: '', descripcion: '', activo: true, tiempoMaximoMinutos: '', maxEjecucionesGlobales: '' },
  toEditValues: (servicio) => ({
    nombre: servicio.nombre,
    descripcion: servicio.descripcion ?? '',
    activo: servicio.activo,
    tiempoMaximoMinutos: servicio.tiempoMaximoMinutos?.toString() ?? '',
    maxEjecucionesGlobales: servicio.maxEjecucionesGlobales?.toString() ?? '',
  }),
  toCreateInput: (values) => ({
    nombre: values.nombre,
    descripcion: values.descripcion || null,
    tiempoMaximoMinutos: aNumero(values.tiempoMaximoMinutos),
    maxEjecucionesGlobales: aNumero(values.maxEjecucionesGlobales),
  }),
  // Emptying a box on an existing service has to remove the limit, which "null" alone would not do.
  toUpdateInput: (values) => {
    const tiempo = aNumero(values.tiempoMaximoMinutos)
    const global = aNumero(values.maxEjecucionesGlobales)
    return {
      nombre: values.nombre,
      descripcion: values.descripcion || null,
      activo: values.activo,
      tiempoMaximoMinutos: tiempo,
      quitarTiempoMaximo: tiempo === null,
      maxEjecucionesGlobales: global,
      quitarLimiteGlobal: global === null,
    }
  },
}

export function ServiciosPage() {
  return (
    <CrudPage<ServicioDto, ServicioFormValues, rpaApi.CreateServicioInput, rpaApi.UpdateServicioInput>
      title="Servicios"
      resourceKey="servicios-crud"
      getId={(servicio) => servicio.id}
      columns={columns}
      form={form}
      filters={{ mode: 'general', placeholder: 'Nombre del servicio…' }}
      api={{
        list: rpaApi.listServicios,
        create: rpaApi.createServicio,
        update: rpaApi.updateServicio,
        remove: rpaApi.deleteServicio,
      }}
      deleteConfirm={{
        message: '¿Seguro que quieres eliminar este servicio? Esta acción no se puede deshacer.',
      }}
    />
  )
}
