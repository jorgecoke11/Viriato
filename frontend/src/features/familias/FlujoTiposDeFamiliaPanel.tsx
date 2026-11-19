import { useQuery } from '@tanstack/react-query'
import { useMemo } from 'react'
import { Link } from 'react-router-dom'
import { CrudPage } from '../../components/crud/CrudPage'
import type { CrudColumn, CrudField, CrudFormConfig } from '../../components/crud/types'
import { Badge } from '../../components/ui/Badge'
import * as flujosApi from '../flujos/api'
import * as api from './api'
import type { CondicionDeCierre, GuardarTipoDeFamiliaRequest, TipoDeFamiliaDto } from './api'

interface ValoresDeTipo {
  [key: string]: string | boolean
  nombre: string
  descripcion: string
  activo: boolean
  creadorAlTerminarId: string
  condicion: string
}

const VACIOS: ValoresDeTipo = { nombre: '', descripcion: '', activo: true, creadorAlTerminarId: '', condicion: 'Siempre' }

const aSolicitud = (v: ValoresDeTipo): GuardarTipoDeFamiliaRequest => ({
  nombre: v.nombre.trim(),
  descripcion: v.descripcion.trim() || null,
  activo: v.activo,
  creadorAlTerminarId: v.creadorAlTerminarId || null,
  condicion: (v.condicion || 'Siempre') as CondicionDeCierre,
})

/**
 * The kinds of family a process works with. A family groups cases that end together (e.g. every extraction of a day); its type says
 * what happens when one ends: open a closing case with one of the process's creators, always or only if every case went well. The
 * families themselves are made by robots (or people) as they create cases: see Familias.
 */
export function FlujoTiposDeFamiliaPanel({ flujoId }: { flujoId: string }) {
  const creadoresQuery = useQuery({ queryKey: ['flujo-creadores', flujoId], queryFn: () => flujosApi.listCreadoresDeCaso(flujoId) })

  const campos = useMemo<CrudField<ValoresDeTipo>[]>(
    () => [
      { name: 'nombre', label: 'Nombre', required: true, helpText: 'Los robots lo nombran al crear una familia, p. ej. «Extracción del día».' },
      { name: 'descripcion', label: 'Descripción', type: 'textarea' },
      {
        name: 'creadorAlTerminarId',
        label: 'Al terminar una familia, abrir un caso con',
        type: 'select',
        options: [
          { value: '', label: 'Nada (solo los avisos que la escuchen)' },
          ...(creadoresQuery.data ?? []).map((c) => ({ value: c.id, label: c.nombre })),
        ],
        helpText: 'El caso de cierre recibe en sus datos «familia»: su nombre, cuántos casos acabaron de cada forma y la lista de casos.',
      },
      {
        name: 'condicion',
        label: 'Abrirlo',
        type: 'select',
        options: api.CONDICIONES,
        visibleWhen: (v) => v.creadorAlTerminarId !== '',
      },
      { name: 'activo', label: 'Activo', type: 'checkbox', helpText: 'Un tipo desactivado no admite familias nuevas; las que tiene siguen su curso.' },
    ],
    [creadoresQuery.data],
  )

  const columnas: CrudColumn<TipoDeFamiliaDto>[] = [
    {
      key: 'nombre',
      label: 'Tipo',
      render: (t) => (
        <span className="flex flex-col">
          <span className="font-medium text-gray-900">{t.nombre}</span>
          {t.descripcion && <span className="text-xs text-gray-500">{t.descripcion}</span>}
        </span>
      ),
    },
    {
      key: 'cierre',
      label: 'Al terminar',
      render: (t) =>
        t.creadorAlTerminarNombre ? (
          <span className="flex flex-col">
            <span className="text-sm text-gray-700">Abre un caso con «{t.creadorAlTerminarNombre}»</span>
            <span className="text-xs text-gray-500">{t.condicion === 'SoloSiTodosBien' ? 'Solo si todos acabaron bien' : 'Siempre'}</span>
          </span>
        ) : (
          <span className="text-sm text-gray-500">Nada</span>
        ),
    },
    {
      key: 'familias',
      label: 'Familias',
      render: (t) => (
        <Link to={`/familias?flujoId=${flujoId}&tipoDeFamiliaId=${t.id}`} className="text-sm font-medium text-indigo-600 hover:text-indigo-700">
          Ver familias
        </Link>
      ),
    },
    { key: 'activo', label: 'Estado', render: (t) => (t.activo ? <Badge tone="success">Activo</Badge> : <Badge tone="neutral">Inactivo</Badge>) },
  ]

  const form: CrudFormConfig<TipoDeFamiliaDto, ValoresDeTipo, GuardarTipoDeFamiliaRequest, GuardarTipoDeFamiliaRequest> = {
    createFields: campos,
    editFields: campos,
    emptyValues: VACIOS,
    toEditValues: (t) => ({
      nombre: t.nombre,
      descripcion: t.descripcion ?? '',
      activo: t.activo,
      creadorAlTerminarId: t.creadorAlTerminarId ?? '',
      condicion: t.condicion,
    }),
    toCreateInput: aSolicitud,
    toUpdateInput: aSolicitud,
  }

  return (
    <CrudPage<TipoDeFamiliaDto, ValoresDeTipo, GuardarTipoDeFamiliaRequest, GuardarTipoDeFamiliaRequest>
      title="Tipos de familia"
      resourceKey={`flujo-${flujoId}-tipos-de-familia`}
      getId={(t) => t.id}
      columns={columnas}
      form={form}
      filters={{ mode: 'none' }}
      entidad={{ singular: 'tipo de familia', plural: 'tipos de familia' }}
      nombreDeFila={(t) => t.nombre}
      api={{
        list: async () => {
          const tipos = await api.listTipos(flujoId)
          return { items: tipos, page: 1, pageSize: tipos.length || 1, total: tipos.length }
        },
        create: (input) => api.createTipo(flujoId, input),
        update: (id, input) => api.updateTipo(flujoId, id, input),
        remove: (id) => api.deleteTipo(flujoId, id),
      }}
      deleteConfirm={{ message: '¿Eliminar este tipo de familia? Si ya tiene familias no se puede: desactívalo.' }}
    />
  )
}
