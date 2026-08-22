import type { ReactNode } from 'react'
import type { PagedResult } from '../../lib/types'

export type { PagedResult }

export interface CrudColumn<T> {
  key: string
  label: string
  render: (item: T) => ReactNode
}

export interface CrudField<TValues> {
  name: keyof TValues & string
  label: string
  type?: 'text' | 'textarea'
  required?: boolean
}

export interface CrudApi<T, TCreate, TUpdate> {
  list: (search: string) => Promise<PagedResult<T>>
  create?: (input: TCreate) => Promise<T>
  update?: (id: string, input: TUpdate) => Promise<T>
  remove?: (id: string) => Promise<void>
}

export interface CrudFormConfig<T, TFormValues, TCreate, TUpdate> {
  fields: CrudField<TFormValues>[]
  emptyValues: TFormValues
  toEditValues: (item: T) => TFormValues
  toCreateInput: (values: TFormValues) => TCreate
  toUpdateInput: (values: TFormValues) => TUpdate
}
