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
  { value: 'S3Compatible', label: 'S3-compatible (MinIO, S3, R2, B2)' },
  { value: 'Local', label: 'Disco local' },
]

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

const createFields: CrudFormConfig<
  StorageConfigDto,
  StorageConfigFormValues,
  flujosApi.CreateStorageConfigRequest,
  flujosApi.UpdateStorageConfigRequest
>['createFields'] = [
  { name: 'nombre', label: 'Nombre', required: true },
  { name: 'proveedor', label: 'Proveedor', type: 'select', required: true, options: PROVEEDOR_OPTIONS },
  { name: 'endpoint', label: 'Endpoint', helpText: 'Solo para S3-compatible, ej. https://minio.tu-dominio.com' },
  { name: 'bucketName', label: 'Bucket', helpText: 'Solo para S3-compatible' },
  { name: 'region', label: 'Región', helpText: 'Solo para S3-compatible. MinIO no la usa pero el SDK pide un valor, ej. us-east-1' },
  { name: 'accessKey', label: 'Access key', type: 'password', helpText: 'Solo para S3-compatible' },
  { name: 'secretKey', label: 'Secret key', type: 'password', helpText: 'Solo para S3-compatible' },
  { name: 'usePathStyle', label: 'Direccionamiento por path (necesario para MinIO)', type: 'checkbox' },
  { name: 'useSsl', label: 'Usar SSL', type: 'checkbox' },
  { name: 'localPath', label: 'Ruta local', helpText: 'Solo para almacenamiento en disco local' },
]

const editFields: CrudFormConfig<
  StorageConfigDto,
  StorageConfigFormValues,
  flujosApi.CreateStorageConfigRequest,
  flujosApi.UpdateStorageConfigRequest
>['editFields'] = [
  { name: 'nombre', label: 'Nombre', required: true },
  { name: 'endpoint', label: 'Endpoint', helpText: 'Solo para S3-compatible' },
  { name: 'bucketName', label: 'Bucket', helpText: 'Solo para S3-compatible' },
  { name: 'region', label: 'Región', helpText: 'Solo para S3-compatible' },
  { name: 'accessKey', label: 'Access key', type: 'password', helpText: 'Déjalo en blanco para no cambiarla' },
  { name: 'secretKey', label: 'Secret key', type: 'password', helpText: 'Déjala en blanco para no cambiarla' },
  { name: 'usePathStyle', label: 'Direccionamiento por path (necesario para MinIO)', type: 'checkbox' },
  { name: 'useSsl', label: 'Usar SSL', type: 'checkbox' },
  { name: 'localPath', label: 'Ruta local', helpText: 'Solo para almacenamiento en disco local' },
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
    proveedor: 'S3Compatible',
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
  toCreateInput: (values) => ({
    nombre: values.nombre,
    proveedor: values.proveedor,
    endpoint: values.endpoint || null,
    region: values.region || null,
    bucketName: values.bucketName || null,
    accessKey: values.accessKey || null,
    secretKey: values.secretKey || null,
    usePathStyle: values.usePathStyle,
    useSsl: values.useSsl,
    localPath: values.localPath || null,
  }),
  toUpdateInput: (values) => ({
    nombre: values.nombre,
    endpoint: values.endpoint || null,
    region: values.region || null,
    bucketName: values.bucketName || null,
    accessKey: values.accessKey || undefined,
    secretKey: values.secretKey || undefined,
    usePathStyle: values.usePathStyle,
    useSsl: values.useSsl,
    localPath: values.localPath || null,
    activo: values.activo,
  }),
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
