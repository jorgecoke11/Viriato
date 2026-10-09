import { Link } from 'react-router-dom'
import type { ColumnaDeTabla } from '../../../components/ui/DataTable'
import { ListaDeDatos } from '../../../components/ui/ListaDeDatos'
import type { ConsultaDeLista } from '../../../lib/lista'
import { useListaPaginada } from '../../../lib/useListaPaginada'
import * as opsApi from '../api'
import type { TrabajoListItemDto } from '../api'
import { StatusBadge } from '../StatusBadge'

const STATUSES = ['Pending', 'Running', 'Completed', 'Failed']

const obtenerId = (t: TrabajoListItemDto) => t.id

const cargar = ({ filtros, pagina, tamano }: ConsultaDeLista) =>
  opsApi.listTrabajos({ processCode: filtros.processCode, subjectKey: filtros.subjectKey, status: filtros.status, page: pagina, pageSize: tamano })

const columnas: ColumnaDeTabla<TrabajoListItemDto>[] = [
  { clave: 'proceso', titulo: 'Proceso', celda: (t) => <span className="font-mono text-xs">{t.processCode}</span> },
  { clave: 'sujeto', titulo: 'Sujeto', celda: (t) => <span className="text-gray-600">{t.subjectType ? `${t.subjectType}: ${t.subjectKey}` : '—'}</span> },
  { clave: 'estado', titulo: 'Estado', celda: (t) => <StatusBadge status={t.status} /> },
  { clave: 'progreso', titulo: 'Progreso', celda: (t) => <span className="text-gray-600">{t.progress !== null ? `${t.progress}%` : '—'}</span> },
  { clave: 'resumen', titulo: 'Resumen', celda: (t) => <span className="text-gray-600">{t.summary ?? '—'}</span> },
  { clave: 'iniciado', titulo: 'Iniciado', celda: (t) => <span className="text-gray-500">{new Date(t.startedAt).toLocaleString()}</span> },
  {
    clave: 'ver',
    titulo: '',
    celda: (t) => (
      <Link to={`/ops/trabajos/${t.id}`} className="text-gray-500 hover:text-gray-900">
        Ver
      </Link>
    ),
  },
]

export function TrabajosPage() {
  const fuente = useListaPaginada<TrabajoListItemDto>({
    clave: ['ops-trabajos'],
    cargar,
    obtenerId,
    refrescarCada: (datos) => (datos?.items.some((t) => t.status === 'Running') ? 3000 : false),
  })

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="page-title">Trabajos</h1>
        <p className="text-sm text-gray-500">Monitor genérico de cualquier scraping o script lanzado en la plataforma.</p>
      </div>

      <ListaDeDatos
        fuente={fuente}
        columnas={columnas}
        obtenerId={obtenerId}
        nombreDeFila={(t) => t.processCode}
        entidad={{ singular: 'trabajo', plural: 'trabajos' }}
        buscador={false}
        filtros={[
          { clave: 'processCode', etiqueta: 'Proceso', tipo: 'texto', placeholder: 'Proceso: markets.sync…' },
          { clave: 'subjectKey', etiqueta: 'Sujeto', tipo: 'texto', placeholder: 'Sujeto: AAPL…' },
          {
            clave: 'status',
            etiqueta: 'Estado',
            opciones: [{ valor: '', etiqueta: 'Todos los estados' }, ...STATUSES.map((s) => ({ valor: s, etiqueta: s }))],
          },
        ]}
        vacio={{ titulo: 'Sin trabajos todavía' }}
        anchoMinimo="720px"
      />
    </div>
  )
}
