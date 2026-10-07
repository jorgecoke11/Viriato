import { useMutation, useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button } from '../../components/ui/Button'
import { Modal } from '../../components/ui/Modal'
import { ApiError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import * as flujosApi from '../flujos/api'
import * as rpaApi from '../rpa/api'
import * as casosApi from './api'
import { DatosCasoInput } from './DatosCasoInput'

/**
 * The same "start a Caso" form as NuevoCasoPage.tsx, but for the case where the Proceso is already
 * known (opened from that Proceso's own action icon) — so there's no Flujo picker, just its name
 * shown as context. The generic page (any Flujo, reached from Listado) stays a separate page: it
 * has no single Proceso to be "inside", so a full page with its own picker still makes more sense there.
 */
export function NuevoCasoModal({
  open,
  flujoId,
  flujoNombre,
  onClose,
}: {
  open: boolean
  flujoId: string
  flujoNombre: string
  onClose: () => void
}) {
  const navigate = useNavigate()
  const { showToast } = useToast()

  const [estadoNegocioInicialId, setEstadoNegocioInicialId] = useState('')
  const [tipoCasoId, setTipoCasoId] = useState('')
  const [titulo, setTitulo] = useState('')
  const [pasoInicialId, setPasoInicialId] = useState('')
  const [datosJson, setDatosJson] = useState('')
  const [datosErrores, setDatosErrores] = useState<string[]>([])
  const [intentoEnvio, setIntentoEnvio] = useState(false)

  useEffect(() => {
    if (open) {
      setEstadoNegocioInicialId('')
      setTipoCasoId('')
      setPasoInicialId('')
      setTitulo('')
      setDatosJson('')
      setDatosErrores([])
      setIntentoEnvio(false)
    }
  }, [open])

  const tiposCasoQuery = useQuery({
    queryKey: ['flujo-tipos-caso', flujoId],
    queryFn: () => flujosApi.listFlujoTiposCaso(flujoId),
    enabled: open,
  })
  const tiposCaso = (tiposCasoQuery.data ?? []).filter((t) => t.activo).sort((a, b) => a.orden - b.orden)

  const tipoSeleccionado = tiposCaso.find((t) => t.id === tipoCasoId)

  // The servicio a Caso starts at: only servicio-backed (Rpa) steps of the process's active version are offered —
  // the rest (Api, Decision, Interno…) is plumbing the flow's author wires up.
  const flujosQuery = useQuery({ queryKey: ['flujos-asignados'], queryFn: casosApi.listFlujosAsignados, enabled: open })
  const versionActivaId = flujosQuery.data?.find((f) => f.id === flujoId)?.versionActivaId
  const versionQuery = useQuery({
    queryKey: ['flujo-version-activa', versionActivaId],
    queryFn: () => casosApi.getFlujoVersion(flujoId, versionActivaId!),
    enabled: open && Boolean(versionActivaId),
  })
  const serviciosQuery = useQuery({ queryKey: ['servicios-all'], queryFn: () => rpaApi.listServicios(), enabled: open })
  const pasosDeServicio = (versionQuery.data?.pasos ?? [])
    .filter((p) => p.tipoPaso === 'Rpa' && p.servicioId)
    .sort((a, b) => a.orden - b.orden)
  const nombreServicio = (servicioId: string) => serviciosQuery.data?.items.find((s) => s.id === servicioId)?.nombre ?? '—'

  const estadosQuery = useQuery({
    queryKey: ['flujo-estados', flujoId],
    queryFn: () => flujosApi.listFlujoEstados(flujoId),
    enabled: open,
  })
  const estados = (estadosQuery.data ?? []).filter((e) => e.activo).sort((a, b) => a.orden - b.orden)

  const crear = useMutation({
    mutationFn: () =>
      casosApi.startCaso({
        flujoId,
        titulo: titulo.trim(),
        datosJson: datosJson.trim() || null,
        estadoNegocioInicialId: estadoNegocioInicialId || null,
        tipoCasoId: tipoCasoId || null,
        pasoInicialId: pasoInicialId || null,
      }),
    onSuccess: (caso) => {
      showToast('success', 'Caso creado correctamente.')
      onClose()
      navigate(`/casos/${caso.id}`)
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo crear el caso.'),
  })

  // Each type can have its own form, so changing it starts the data over.
  function handleTipoChange(value: string) {
    setTipoCasoId(value)
    setDatosJson('')
    setDatosErrores([])
    setIntentoEnvio(false)
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (datosErrores.length > 0) {
      setIntentoEnvio(true)
      return
    }

    crear.mutate()
  }

  return (
    <Modal open={open} title={`Nuevo caso — ${flujoNombre}`} onClose={onClose}>
      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <div className="flex flex-col gap-1">
          <label htmlFor="titulo-modal" className="text-sm font-medium text-gray-700">Título</label>
          <input
            id="titulo-modal"
            required
            value={titulo}
            onChange={(e) => setTitulo(e.target.value)}
            placeholder="Expediente 2026-014…"
            className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
          />
        </div>

        {tiposCaso.length > 0 && (
          <div className="flex flex-col gap-1">
            <label htmlFor="tipoCaso-modal" className="text-sm font-medium text-gray-700">Tipo de caso</label>
            <select
              id="tipoCaso-modal"
              className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
              value={tipoCasoId}
              onChange={(e) => handleTipoChange(e.target.value)}
            >
              <option value="">Sin tipo</option>
              {tiposCaso.map((t) => (
                <option key={t.id} value={t.id}>{t.nombre}</option>
              ))}
            </select>
          </div>
        )}

        {pasosDeServicio.length > 0 && (
          <div className="flex flex-col gap-1">
            <label htmlFor="pasoInicial-modal" className="text-sm font-medium text-gray-700">Servicio a lanzar primero</label>
            <select
              id="pasoInicial-modal"
              className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
              value={pasoInicialId}
              onChange={(e) => setPasoInicialId(e.target.value)}
              disabled={versionQuery.isLoading || serviciosQuery.isLoading}
            >
              <option value="">Primer paso del flujo (por defecto)</option>
              {pasosDeServicio.map((paso) => (
                <option key={paso.id} value={paso.id}>
                  {paso.nombre} ({nombreServicio(paso.servicioId!)})
                </option>
              ))}
            </select>
            <p className="text-xs text-gray-500">Los pasos anteriores al elegido quedan marcados como omitidos.</p>
          </div>
        )}

        {estados.length > 0 && (
          <div className="flex flex-col gap-1">
            <label htmlFor="estadoNegocioInicial-modal" className="text-sm font-medium text-gray-700">Estado de negocio inicial</label>
            <select
              id="estadoNegocioInicial-modal"
              className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
              value={estadoNegocioInicialId}
              onChange={(e) => setEstadoNegocioInicialId(e.target.value)}
              disabled={estadosQuery.isLoading}
            >
              <option value="">Sin estado</option>
              {estados.map((e) => (
                <option key={e.id} value={e.id}>{e.display}</option>
              ))}
            </select>
          </div>
        )}

        <DatosCasoInput
          key={tipoCasoId}
          esquemaJson={tipoSeleccionado?.esquemaDatosJson}
          value={datosJson}
          onChange={(json, errores) => {
            setDatosJson(json)
            setDatosErrores(errores)
          }}
          mostrarErrores={intentoEnvio}
        />

        <div className="flex justify-end gap-2 border-t border-gray-100 pt-4">
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancelar
          </Button>
          <Button type="submit" disabled={titulo.trim() === '' || crear.isPending}>
            {crear.isPending ? 'Creando…' : 'Crear caso'}
          </Button>
        </div>
      </form>
    </Modal>
  )
}
