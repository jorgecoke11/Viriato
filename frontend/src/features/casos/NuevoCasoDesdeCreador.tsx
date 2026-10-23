import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '../../components/ui/Button'
import { Input } from '../../components/ui/Input'
import { ApiError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import * as casosApi from './api'
import type { CasoListItemDto, CreadorDisponibleDto } from './api'
import { DatosConFormulario } from '../../components/schema-form/DatosConFormulario'

/**
 * What a person fills in to create a case once they have picked a creator: the data (the creator's form, starting from its
 * defaults, or JSON if it has none) and, if they want, a title of their own. The title is written by the creator when left empty — its example is shown
 * as the placeholder so nobody wonders what they would get.
 */
export function NuevoCasoDesdeCreador({
  creador,
  alCrear,
  alCancelar,
}: {
  creador: CreadorDisponibleDto
  alCrear: (caso: CasoListItemDto) => void
  alCancelar?: () => void
}) {
  const { showToast } = useToast()
  const queryClient = useQueryClient()
  const [titulo, setTitulo] = useState('')
  // The creator's defaults are where the form starts; what the person then changes or empties is what is sent.
  const [datosJson, setDatosJson] = useState(creador.datosPorDefectoJson ?? '')
  const [datosErrores, setDatosErrores] = useState<string[]>([])
  const [intentoEnvio, setIntentoEnvio] = useState(false)

  const crear = useMutation({
    mutationFn: () => casosApi.crearCasoDesdeCreador(creador.id, { titulo: titulo.trim() || null, datosJson: datosJson.trim() || null }),
    onSuccess: (caso) => {
      showToast('success', `Se ha creado el caso «${caso.titulo}» correctamente.`)
      for (const clave of ['casos', 'casos-resumen', 'cola-equipo']) void queryClient.invalidateQueries({ queryKey: [clave] })
      alCrear(caso)
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo crear el caso.'),
  })

  function enviar(e: React.FormEvent) {
    e.preventDefault()
    if (datosErrores.length > 0) {
      setIntentoEnvio(true)
      return
    }
    crear.mutate()
  }

  return (
    <form onSubmit={enviar} className="flex flex-col gap-4">
      <DatosConFormulario
        key={creador.id}
        esquemaJson={creador.esquemaDatosJson}
        value={datosJson}
        onChange={(json, errores) => {
          setDatosJson(json)
          setDatosErrores(errores)
        }}
        mostrarErrores={intentoEnvio}
      />

      <Input
        label="Título"
        name="titulo"
        value={titulo}
        onChange={(e) => setTitulo(e.target.value)}
        placeholder={creador.tituloEjemplo}
        hint="Se genera solo al crear el caso. Escríbelo solo si quieres otro."
      />

      <div className="flex justify-end gap-2 border-t border-gray-100 pt-4">
        {alCancelar && (
          <Button type="button" variant="ghost" onClick={alCancelar}>
            Cancelar
          </Button>
        )}
        <Button type="submit" disabled={crear.isPending}>
          {crear.isPending ? 'Creando…' : 'Crear caso'}
        </Button>
      </div>
    </form>
  )
}
