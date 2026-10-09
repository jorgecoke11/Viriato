import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { Button } from '../../components/ui/Button'
import { Input } from '../../components/ui/Input'
import { ApiError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import * as casosApi from './api'
import type { CasoListItemDto, CreadorDisponibleDto } from './api'
import { DatosCasoInput } from './DatosCasoInput'

/**
 * What a person fills in to create a case once they have picked a creator: the data (a form if the creator's type has one, JSON
 * if not) and, if they want, a title of their own. The title is written by the creator when left empty — its example is shown
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
  const [titulo, setTitulo] = useState('')
  const [datosJson, setDatosJson] = useState('')
  const [datosErrores, setDatosErrores] = useState<string[]>([])
  const [intentoEnvio, setIntentoEnvio] = useState(false)

  const crear = useMutation({
    mutationFn: () => casosApi.crearCasoDesdeCreador(creador.id, { titulo: titulo.trim() || null, datosJson: datosJson.trim() || null }),
    onSuccess: (caso) => {
      showToast('success', `Caso «${caso.titulo}» creado.`)
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
      <DatosCasoInput
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
