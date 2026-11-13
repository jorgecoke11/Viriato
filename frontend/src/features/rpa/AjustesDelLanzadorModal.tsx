import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Button } from '../../components/ui/Button'
import { Input } from '../../components/ui/Input'
import { Modal } from '../../components/ui/Modal'
import { mensajeDeError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import * as rpaApi from './api'
import type { AjustesDelLanzadorDto } from './api'

interface Props {
  open: boolean
  onClose: () => void
}

/** How much the launcher next to Viriato may run at once on its machine. Read by the launcher every round. */
export function AjustesDelLanzadorModal({ open, onClose }: Props) {
  const ajustesQuery = useQuery({ queryKey: ['ajustes-del-lanzador'], queryFn: rpaApi.getAjustesDelLanzador, enabled: open })

  return (
    <Modal open={open} title="Lanzador de robots" onClose={onClose}>
      {ajustesQuery.isError && <p className="text-sm text-red-600">No se han podido cargar los ajustes.</p>}
      {ajustesQuery.data && <Formulario key={JSON.stringify(ajustesQuery.data)} ajustes={ajustesQuery.data} onClose={onClose} />}
    </Modal>
  )
}

function Formulario({ ajustes, onClose }: { ajustes: AjustesDelLanzadorDto; onClose: () => void }) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [robots, setRobots] = useState(String(ajustes.maxRobotsALaVez))
  const [conNavegador, setConNavegador] = useState(String(ajustes.maxConNavegador))

  const guardar = useMutation({
    mutationFn: () => rpaApi.guardarAjustesDelLanzador({ maxRobotsALaVez: Number(robots), maxConNavegador: Number(conNavegador) }),
    onSuccess: (guardados) => {
      queryClient.setQueryData(['ajustes-del-lanzador'], guardados)
      showToast('success', 'Guardado. El lanzador lo aplica en unos segundos.')
      onClose()
    },
    onError: (err) => showToast('error', mensajeDeError(err, 'No se pudo guardar.')),
  })

  function alGuardar(event: FormEvent) {
    event.preventDefault()
    guardar.mutate()
  }

  return (
    <form onSubmit={alGuardar} className="flex flex-col gap-4">
      <p className="text-sm text-gray-600">
        Cuánto puede tener arrancado a la vez el lanzador de Viriato. Si hay más trabajo, los robots esperan su turno en el orden en que se
        crearon sus despliegues.
      </p>
      <Input
        label="Robots a la vez"
        type="number"
        inputMode="numeric"
        min={1}
        max={20}
        required
        value={robots}
        hint="Copias de cualquier robot en marcha al mismo tiempo."
        onChange={(e) => setRobots(e.target.value)}
      />
      <Input
        label="De ellos, con navegador"
        type="number"
        inputMode="numeric"
        min={0}
        max={20}
        required
        value={conNavegador}
        hint="Son los que pesan: unos 2 GB cada uno. Solo el primero se ve en directo desde fuera."
        onChange={(e) => setConNavegador(e.target.value)}
      />
      <div className="flex justify-end gap-2">
        <Button type="button" variant="ghost" onClick={onClose}>
          Cancelar
        </Button>
        <Button type="submit" disabled={guardar.isPending}>
          {guardar.isPending ? 'Guardando…' : 'Guardar'}
        </Button>
      </div>
    </form>
  )
}
