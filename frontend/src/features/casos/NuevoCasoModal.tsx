import { useMutation, useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button } from '../../components/ui/Button'
import { Modal } from '../../components/ui/Modal'
import { ApiError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import * as flujosApi from '../flujos/api'
import * as casosApi from './api'

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
  const [datosJson, setDatosJson] = useState('')
  const [datosJsonError, setDatosJsonError] = useState<string | null>(null)

  useEffect(() => {
    if (open) {
      setEstadoNegocioInicialId('')
      setTipoCasoId('')
      setTitulo('')
      setDatosJson('')
      setDatosJsonError(null)
    }
  }, [open])

  const tiposCasoQuery = useQuery({
    queryKey: ['flujo-tipos-caso', flujoId],
    queryFn: () => flujosApi.listFlujoTiposCaso(flujoId),
    enabled: open,
  })
  const tiposCaso = (tiposCasoQuery.data ?? []).filter((t) => t.activo).sort((a, b) => a.orden - b.orden)

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
      }),
    onSuccess: (caso) => {
      showToast('success', 'Caso creado correctamente.')
      onClose()
      navigate(`/casos/${caso.id}`)
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo crear el caso.'),
  })

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setDatosJsonError(null)

    if (datosJson.trim()) {
      try {
        JSON.parse(datosJson)
      } catch {
        setDatosJsonError('El JSON no es válido — revisa la sintaxis.')
        return
      }
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
              onChange={(e) => setTipoCasoId(e.target.value)}
            >
              <option value="">Sin tipo</option>
              {tiposCaso.map((t) => (
                <option key={t.id} value={t.id}>{t.nombre}</option>
              ))}
            </select>
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

        <div className="flex flex-col gap-1">
          <label htmlFor="datosJson-modal" className="text-sm font-medium text-gray-700">
            Datos de negocio <span className="font-normal text-gray-400">(opcional)</span>
          </label>
          <textarea
            id="datosJson-modal"
            rows={4}
            className="rounded-lg border border-gray-300 px-3 py-2 font-mono text-xs focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
            value={datosJson}
            onChange={(e) => {
              setDatosJson(e.target.value)
              setDatosJsonError(null)
            }}
            placeholder={'{\n  "cliente": "ACME Corp"\n}'}
          />
          {datosJsonError && <span className="text-sm text-red-600">{datosJsonError}</span>}
        </div>

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
