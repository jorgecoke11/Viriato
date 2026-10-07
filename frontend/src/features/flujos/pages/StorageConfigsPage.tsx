import { CrudPage } from '../../../components/crud/CrudPage'
import type { CrudColumn, CrudFormConfig } from '../../../components/crud/types'
import * as flujosApi from '../api'
import type { StorageConfigDto, StorageProviderType } from '../api'

interface StorageConfigFormValues {
  [key: string]: string | boolean
  nombre: string
  proveedor: StorageProviderType
  endpoint: string
  region: string
  bucketName: string
  accessKey: string
  secretKey: string
  usePathStyle: boolean
  useSsl: boolean
  localPath: string
  activo: boolean
}

const PROVEEDOR_OPTIONS = [
  { value: 'Local', label: 'Disco local' },
  { value: 'S3Compatible', label: 'S3-compatible (MinIO, S3, R2, B2)' },
]

const LOCAL_PATH_HELP =
  'Es la ruta dentro del contenedor de la API (p. ej. /data/storage/expedientes) y debe colgar de una carpeta montada en docker-compose; si no, los archivos se pierden al recrear el contenedor. Dentro se organiza solo: documentos/ y evidencias/ por año, mes y caso.'

const columns: CrudColumn<StorageConfigDto>[] = [
  { key: 'nombre', label: 'Nombre', render: (s) => s.nombre },
  {
    key: 'proveedor',
    label: 'Proveedor',
    render: (s) => (s.proveedor === 'S3Compatible' ? 'S3-compatible' : 'Disco local'),
  },
  {
    key: 'destino',
    label: 'Destino',
    render: (s) => (s.proveedor === 'S3Compatible' ? `${s.endpoint ?? '—'} / ${s.bucketName ?? '—'}` : (s.localPath ?? '—')),
  },
  {
    key: 'credenciales',
    label: 'Credenciales',
    render: (s) => (s.proveedor === 'S3Compatible' ? (s.hasCredentials ? 'Configuradas' : 'Sin configurar') : '—'),
  },
  { key: 'activo', label: 'Activo', render: (s) => (s.activo ? 'Sí' : 'No') },
]

const isLocal = (values: StorageConfigFormValues) => values.proveedor === 'Local'
const isS3 = (values: StorageConfigFormValues) => values.proveedor === 'S3Compatible'

const createFields: CrudFormConfig<
  StorageConfigDto,
  StorageConfigFormValues,
  flujosApi.CreateStorageConfigRequest,
  flujosApi.UpdateStorageConfigRequest
>['createFields'] = [
  { name: 'nombre', label: 'Nombre', required: true },
  { name: 'proveedor', label: 'Proveedor', type: 'select', required: true, options: PROVEEDOR_OPTIONS },
  { name: 'localPath', label: 'Ruta local', required: isLocal, visibleWhen: isLocal, helpText: LOCAL_PATH_HELP },
  { name: 'endpoint', label: 'Endpoint', required: isS3, visibleWhen: isS3, helpText: 'Ej. https://minio.tu-dominio.com' },
  { name: 'bucketName', label: 'Bucket', required: isS3, visibleWhen: isS3 },
  { name: 'region', label: 'Región', visibleWhen: isS3, helpText: 'MinIO no la usa pero el SDK pide un valor, ej. us-east-1' },
  { name: 'accessKey', label: 'Access key', type: 'password', required: isS3, visibleWhen: isS3 },
  { name: 'secretKey', label: 'Secret key', type: 'password', required: isS3, visibleWhen: isS3 },
  { name: 'usePathStyle', label: 'Direccionamiento por path (necesario para MinIO)', type: 'checkbox', visibleWhen: isS3 },
  { name: 'useSsl', label: 'Usar SSL', type: 'checkbox', visibleWhen: isS3 },
]

const editFields: CrudFormConfig<
  StorageConfigDto,
  StorageConfigFormValues,
  flujosApi.CreateStorageConfigRequest,
  flujosApi.UpdateStorageConfigRequest
>['editFields'] = [
  { name: 'nombre', label: 'Nombre', required: true },
  { name: 'localPath', label: 'Ruta local', required: isLocal, visibleWhen: isLocal, helpText: LOCAL_PATH_HELP },
  { name: 'endpoint', label: 'Endpoint', visibleWhen: isS3 },
  { name: 'bucketName', label: 'Bucket', visibleWhen: isS3 },
  { name: 'region', label: 'Región', visibleWhen: isS3 },
  { name: 'accessKey', label: 'Access key', type: 'password', visibleWhen: isS3, helpText: 'Déjalo en blanco para no cambiarla' },
  { name: 'secretKey', label: 'Secret key', type: 'password', visibleWhen: isS3, helpText: 'Déjala en blanco para no cambiarla' },
  { name: 'usePathStyle', label: 'Direccionamiento por path (necesario para MinIO)', type: 'checkbox', visibleWhen: isS3 },
  { name: 'useSsl', label: 'Usar SSL', type: 'checkbox', visibleWhen: isS3 },
  { name: 'activo', label: 'Activo', type: 'checkbox' },
]

const form: CrudFormConfig<
  StorageConfigDto,
  StorageConfigFormValues,
  flujosApi.CreateStorageConfigRequest,
  flujosApi.UpdateStorageConfigRequest
> = {
  createFields,
  editFields,
  emptyValues: {
    nombre: '',
    proveedor: 'Local',
    endpoint: '',
    region: '',
    bucketName: '',
    accessKey: '',
    secretKey: '',
    usePathStyle: true,
    useSsl: true,
    localPath: '',
    activo: true,
  },
  toEditValues: (config) => ({
    nombre: config.nombre,
    proveedor: config.proveedor,
    endpoint: config.endpoint ?? '',
    region: config.region ?? '',
    bucketName: config.bucketName ?? '',
    accessKey: '',
    secretKey: '',
    usePathStyle: config.usePathStyle,
    useSsl: config.useSsl,
    localPath: config.localPath ?? '',
    activo: config.activo,
  }),
  // Only what belongs to the chosen provider is sent: values typed under the other one and then
  // abandoned by switching the select must not leak into the saved config.
  toCreateInput: (values) =>
    isLocal(values)
      ? {
          nombre: values.nombre,
          proveedor: values.proveedor,
          endpoint: null,
          region: null,
          bucketName: null,
          accessKey: null,
          secretKey: null,
          usePathStyle: false,
          useSsl: false,
          localPath: values.localPath || null,
        }
      : {
          nombre: values.nombre,
          proveedor: values.proveedor,
          endpoint: values.endpoint || null,
          region: values.region || null,
          bucketName: values.bucketName || null,
          accessKey: values.accessKey || null,
          secretKey: values.secretKey || null,
          usePathStyle: values.usePathStyle,
          useSsl: values.useSsl,
          localPath: null,
        },
  toUpdateInput: (values) =>
    isLocal(values)
      ? { nombre: values.nombre, localPath: values.localPath || null, activo: values.activo }
      : {
          nombre: values.nombre,
          endpoint: values.endpoint || null,
          region: values.region || null,
          bucketName: values.bucketName || null,
          accessKey: values.accessKey || undefined,
          secretKey: values.secretKey || undefined,
          usePathStyle: values.usePathStyle,
          useSsl: values.useSsl,
          activo: values.activo,
        },
}

export function StorageConfigsPage() {
  return (
    <CrudPage<StorageConfigDto, StorageConfigFormValues, flujosApi.CreateStorageConfigRequest, flujosApi.UpdateStorageConfigRequest>
      title="Almacenamiento"
      resourceKey="storage-configs-admin-crud"
      getId={(config) => config.id}
      columns={columns}
      form={form}
      filters={{ mode: 'general', placeholder: 'Nombre del almacenamiento…' }}
      api={{
        list: flujosApi.listStorageConfigs,
        create: flujosApi.createStorageConfig,
        update: flujosApi.updateStorageConfig,
        remove: flujosApi.deleteStorageConfig,
      }}
      deleteConfirm={{
        message: '¿Seguro que quieres eliminar este almacenamiento? Esta acción no se puede deshacer.',
      }}
    />
  )
}
