import { useMutation, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { Button } from '../../../components/ui/Button'
import { Card } from '../../../components/ui/Card'
import { Input } from '../../../components/ui/Input'
import { ApiError } from '../../../lib/apiClient'
import { useToast } from '../../../lib/toast/useToast'
import * as flujosApi from '../../flujos/api'
import * as casosApi from '../api'

export function NuevoCasoPage() {
  const navigate = useNavigate()
  const { showToast } = useToast()
  const [searchParams] = useSearchParams()

  // Arriving from a process card's own "Añadir caso" action pre-selects that process — the user
  // can still change it, this just saves picking it again from the dropdown.
  const [flujoId, setFlujoId] = useState(() => searchParams.get('flujoId') ?? '')
  const [estadoNegocioInicialId, setEstadoNegocioInicialId] = useState('')
  const [tipoCasoId, setTipoCasoId] = useState('')
  const [titulo, setTitulo] = useState('')
  const [datosJson, setDatosJson] = useState('')
  const [datosJsonError, setDatosJsonError] = useState<string | null>(null)

  const flujosQuery = useQuery({ queryKey: ['flujos-asignados'], queryFn: casosApi.listFlujosAsignados })
  const flujosDisponibles = (flujosQuery.data ?? []).filter((f) => f.versionActivaId)

  const tiposCasoQuery = useQuery({
    queryKey: ['flujo-tipos-caso', flujoId],
    queryFn: () => flujosApi.listFlujoTiposCaso(flujoId),
    enabled: Boolean(flujoId),
  })
  const tiposCaso = (tiposCasoQuery.data ?? []).filter((t) => t.activo).sort((a, b) => a.orden - b.orden)

  const estadosQuery = useQuery({
    queryKey: ['flujo-estados', flujoId],
    queryFn: () => flujosApi.listFlujoEstados(flujoId),
    enabled: Boolean(flujoId),
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
      navigate(`/casos/${caso.id}`)
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo crear el caso.'),
  })

  const handleFlujoChange = (value: string) => {
    setFlujoId(value)
    setEstadoNegocioInicialId('')
    setTipoCasoId('')
  }

  const handleSubmit = (e: React.FormEvent) => {
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

  const puedeEnviar = flujoId !== '' && titulo.trim() !== '' && !crear.isPending

  return (
    <div className="flex flex-col gap-4">
      <Link to="/casos/lista" className="text-sm text-gray-500 hover:text-gray-900">← Listado</Link>

      <div>
        <h1 className="text-2xl font-semibold tracking-tight text-gray-900">Nuevo caso</h1>
        <p className="text-sm text-gray-500">Elige el flujo y, si lo necesitas, en qué estado de negocio debe arrancar.</p>
      </div>

      <Card className="max-w-xl">
        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <div className="flex flex-col gap-1">
            <label htmlFor="flujo" className="text-sm font-medium text-gray-700">Flujo</label>
            <select
              id="flujo"
              required
              className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
              value={flujoId}
              onChange={(e) => handleFlujoChange(e.target.value)}
            >
              <option value="">Selecciona un flujo…</option>
              {flujosDisponibles.map((f) => (
                <option key={f.id} value={f.id}>{f.nombre}</option>
              ))}
            </select>
            {flujosQuery.data && flujosDisponibles.length === 0 && (
              <p className="text-sm text-amber-700">
                No tienes ningún flujo asignado con una versión publicada todavía.
              </p>
            )}
          </div>

          <Input
            label="Título"
            name="titulo"
            required
            value={titulo}
            onChange={(e) => setTitulo(e.target.value)}
            placeholder="Expediente 2026-014…"
          />

          {flujoId && tiposCaso.length > 0 && (
            <div className="flex flex-col gap-1">
              <label htmlFor="tipoCaso" className="text-sm font-medium text-gray-700">Tipo de caso</label>
              <select
                id="tipoCaso"
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

          {flujoId && estados.length > 0 && (
            <div className="flex flex-col gap-1">
              <label htmlFor="estadoNegocioInicial" className="text-sm font-medium text-gray-700">Estado de negocio inicial</label>
              <select
                id="estadoNegocioInicial"
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
              <p className="text-xs text-gray-500">
                Es solo informativo — no cambia por dónde arranca el flujo, que siempre empieza en su primer paso.
              </p>
            </div>
          )}

          <div className="flex flex-col gap-1">
            <label htmlFor="datosJson" className="text-sm font-medium text-gray-700">
              Datos de negocio <span className="font-normal text-gray-400">(opcional)</span>
            </label>
            <textarea
              id="datosJson"
              rows={5}
              className="rounded-lg border border-gray-300 px-3 py-2 font-mono text-xs focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
              value={datosJson}
              onChange={(e) => {
                setDatosJson(e.target.value)
                setDatosJsonError(null)
              }}
              placeholder={'{\n  "cliente": "ACME Corp"\n}'}
            />
            <p className="text-xs text-gray-500">JSON con los datos iniciales del caso — puedes dejarlo vacío y añadirlo más tarde.</p>
            {datosJsonError && <span className="text-sm text-red-600">{datosJsonError}</span>}
          </div>

          <div className="flex justify-end gap-2 border-t border-gray-100 pt-4">
            <Button type="submit" disabled={!puedeEnviar}>
              {crear.isPending ? 'Creando…' : 'Crear caso'}
            </Button>
          </div>
        </form>
      </Card>
    </div>
  )
}
