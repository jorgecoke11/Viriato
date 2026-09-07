import type { ReactNode } from 'react'
import type { PagedResult } from '../../lib/types'

export type { PagedResult }

export interface CrudColumn<T> {
  key: string
  label: string
  render: (item: T) => ReactNode
}

export type CrudFieldType = 'text' | 'textarea' | 'number' | 'checkbox' | 'select' | 'password'

export interface CrudFieldOption {
  value: string
  label: string
}

export interface CrudField<TValues> {
  name: keyof TValues & string
  label: string
  type?: CrudFieldType
  required?: boolean
  disabled?: boolean
  options?: CrudFieldOption[]
  helpText?: string
}

export interface CrudFormConfig<T, TFormValues extends Record<string, string | boolean>, TCreate, TUpdate> {
  /** Fields shown in the "Nuevo" modal. */
  createFields: CrudField<TFormValues>[]
  /** Fields shown in the "Editar" modal — can differ from createFields (e.g. an identifier
   * that's fixed once created and therefore only ever appears at creation time). */
  editFields: CrudField<TFormValues>[]
  emptyValues: TFormValues
  toEditValues: (item: T) => TFormValues
  toCreateInput: (values: TFormValues) => TCreate
  toUpdateInput: (values: TFormValues) => TUpdate
}

/** One filter control rendered above the table; its value is sent to `CrudApi.list` under `key`. */
export interface CrudFilterField {
  key: string
  label: string
  type?: 'text' | 'select'
  options?: CrudFieldOption[]
  placeholder?: string
}

/**
 * How a CRUD's table can be filtered — chosen per resource, not hardcoded by the kit:
 * - `general`: one search box; its value travels as `filters.search`.
 * - `fields`: one control per configured field, each its own entry in `filters`.
 * - `none`: no filter UI at all.
 */
export type CrudFilterConfig =
  | { mode: 'none' }
  | { mode: 'general'; placeholder?: string }
  | { mode: 'fields'; fields: CrudFilterField[] }

export interface CrudApi<T, TCreate, TUpdate> {
  list: (filters: Record<string, string>) => Promise<PagedResult<T>>
  create?: (input: TCreate) => Promise<T>
  update?: (id: string, input: TUpdate) => Promise<T>
  remove?: (id: string) => Promise<void>
}
