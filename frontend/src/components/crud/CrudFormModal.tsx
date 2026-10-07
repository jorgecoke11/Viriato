import { useEffect, useId, useState, type FormEvent } from 'react'
import { Button } from '../ui/Button'
import { Input } from '../ui/Input'
import { Modal } from '../ui/Modal'
import type { CrudField } from './types'

interface CrudFormModalProps<TValues extends Record<string, string | boolean>> {
  open: boolean
  title: string
  fields: CrudField<TValues>[]
  initialValues: TValues
  onSubmit: (values: TValues) => void
  onClose: () => void
  submitting: boolean
  error: string | null
}

export function CrudFormModal<TValues extends Record<string, string | boolean>>({
  open,
  title,
  fields,
  initialValues,
  onSubmit,
  onClose,
  submitting,
  error,
}: CrudFormModalProps<TValues>) {
  const formId = useId()
  const [values, setValues] = useState<TValues>(initialValues)
  // Re-seeds the form each time it opens (create vs. edit, or a different row) — the component now
  // stays mounted permanently so it can animate its own exit, so this replaces the fresh state a
  // remount used to give us for free.
  useEffect(() => {
    if (open) setValues(initialValues)
  }, [open, initialValues])

  // Frozen so the panel keeps showing the right title/fields/error while it animates out, even
  // though the caller may already be resetting those props back to their "closed" defaults.
  const [frozen, setFrozen] = useState({ title, fields, error })
  useEffect(() => {
    if (open) setFrozen({ title, fields, error })
  }, [open, title, fields, error])

  function handleChange(name: string, value: string | boolean) {
    setValues((prev) => ({ ...prev, [name]: value }))
  }

  function mensajeDeValidacion(field: CrudField<TValues>): string | null {
    const value = values[field.name]
    return field.validate && typeof value === 'string' && value !== '' ? field.validate(value) : null
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const hayErrores = fields
      .filter((field) => !field.visibleWhen || field.visibleWhen(values))
      .some((field) => mensajeDeValidacion(field) !== null)
    if (hayErrores) return
    onSubmit(values)
  }

  return (
    <Modal
      open={open}
      title={frozen.title}
      onClose={onClose}
      footer={
        <div className="flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancelar
          </Button>
          {/* Lives in the footer, outside the <form> DOM subtree — the `form` attribute wires it back
              up so submitting still works even though the fields scroll independently above it. */}
          <Button type="submit" form={formId} disabled={submitting}>
            {submitting ? 'Guardando…' : 'Guardar'}
          </Button>
        </div>
      }
    >
      <form id={formId} className="flex flex-col gap-4" onSubmit={handleSubmit}>
        {frozen.fields
          .filter((field) => !field.visibleWhen || field.visibleWhen(values))
          .map((field) => {
          const value = values[field.name]
          const required = typeof field.required === 'function' ? field.required(values) : field.required

          if (field.type === 'checkbox') {
            return (
              <label key={field.name} className="flex items-center gap-2 text-sm text-gray-700">
                <input
                  type="checkbox"
                  className="accent-indigo-600"
                  checked={Boolean(value)}
                  disabled={field.disabled}
                  onChange={(e) => handleChange(field.name, e.target.checked)}
                />
                {field.label}
              </label>
            )
          }

          if (field.type === 'select') {
            return (
              <div key={field.name} className="flex flex-col gap-1">
                <label className="text-sm font-medium text-gray-700">{field.label}</label>
                <select
                  className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
                  value={String(value ?? '')}
                  disabled={field.disabled}
                  required={required}
                  onChange={(e) => handleChange(field.name, e.target.value)}
                >
                  {field.options?.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
                {field.helpText && <p className="text-xs text-gray-500">{field.helpText}</p>}
              </div>
            )
          }

          if (field.type === 'textarea') {
            return (
              <div key={field.name} className="flex flex-col gap-1">
                <label className="text-sm font-medium text-gray-700">{field.label}</label>
                <textarea
                  className={`rounded-lg border px-3 py-2 text-sm focus:outline-none focus:ring-2 ${
                    mensajeDeValidacion(field)
                      ? 'border-red-300 focus:border-red-400 focus:ring-red-100'
                      : 'border-gray-300 focus:border-indigo-400 focus:ring-indigo-100'
                  } ${field.mono ? 'font-mono text-xs' : ''}`}
                  rows={field.rows ?? 3}
                  spellCheck={field.mono ? false : undefined}
                  value={String(value ?? '')}
                  required={required}
                  disabled={field.disabled}
                  onChange={(e) => handleChange(field.name, e.target.value)}
                />
                {mensajeDeValidacion(field) && <p className="text-xs text-red-600">{mensajeDeValidacion(field)}</p>}
                {field.helpText && <p className="text-xs text-gray-500">{field.helpText}</p>}
              </div>
            )
          }

          return (
            <div key={field.name} className="flex flex-col gap-1">
              <Input
                label={field.label}
                name={field.name}
                type={field.type === 'number' ? 'number' : field.type === 'password' ? 'password' : 'text'}
                value={String(value ?? '')}
                required={required}
                disabled={field.disabled}
                onChange={(e) => handleChange(field.name, e.target.value)}
              />
              {field.helpText && <p className="text-xs text-gray-500">{field.helpText}</p>}
            </div>
          )
        })}
        {frozen.error && <p className="text-sm text-red-600">{frozen.error}</p>}
      </form>
    </Modal>
  )
}
