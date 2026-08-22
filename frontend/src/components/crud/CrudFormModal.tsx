import { useState, type FormEvent } from 'react'
import { Button } from '../ui/Button'
import { Card } from '../ui/Card'
import { Input } from '../ui/Input'
import type { CrudField } from './types'

export function CrudFormModal<TValues extends Record<string, string>>({
  title,
  fields,
  initialValues,
  onSubmit,
  onClose,
  submitting,
  error,
}: {
  title: string
  fields: CrudField<TValues>[]
  initialValues: TValues
  onSubmit: (values: TValues) => void
  onClose: () => void
  submitting: boolean
  error: string | null
}) {
  const [values, setValues] = useState<TValues>(initialValues)

  function handleChange(name: string, value: string) {
    setValues((prev) => ({ ...prev, [name]: value }))
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    onSubmit(values)
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 px-4">
      <Card className="w-full max-w-md">
        <h2 className="mb-4 text-lg font-medium text-gray-900">{title}</h2>
        <form className="flex flex-col gap-4" onSubmit={handleSubmit}>
          {fields.map((field) =>
            field.type === 'textarea' ? (
              <div key={field.name} className="flex flex-col gap-1">
                <label className="text-sm font-medium text-gray-700">{field.label}</label>
                <textarea
                  className="rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-500 focus:outline-none"
                  value={values[field.name] ?? ''}
                  required={field.required}
                  onChange={(e) => handleChange(field.name, e.target.value)}
                />
              </div>
            ) : (
              <Input
                key={field.name}
                label={field.label}
                name={field.name}
                value={values[field.name] ?? ''}
                required={field.required}
                onChange={(e) => handleChange(field.name, e.target.value)}
              />
            ),
          )}
          {error && <p className="text-sm text-red-600">{error}</p>}
          <div className="flex justify-end gap-2">
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancelar
            </Button>
            <Button type="submit" disabled={submitting}>
              {submitting ? 'Guardando…' : 'Guardar'}
            </Button>
          </div>
        </form>
      </Card>
    </div>
  )
}
