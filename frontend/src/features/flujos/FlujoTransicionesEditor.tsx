import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Background,
  BaseEdge,
  Controls,
  EdgeLabelRenderer,
  getBezierPath,
  Handle,
  MarkerType,
  Position,
  ReactFlow,
  ReactFlowProvider,
  useReactFlow,
  type Connection,
  type Edge,
  type EdgeChange,
  type EdgeProps,
  type Node,
  type NodeChange,
  type NodeProps,
} from '@xyflow/react'
import '@xyflow/react/dist/style.css'
import { useEffect, useMemo, useState, type CSSProperties, type DragEvent, type ReactNode } from 'react'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { mensajeDeError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import * as rpaApi from '../rpa/api'
import * as flujosApi from './api'
import type { ResultadoEjecucion, TipoPaso } from './api'
import { FlujoPasosEditor } from './FlujoPasosEditor'
import {
  ANCHO_COLUMNA,
  avisos as calcularAvisos,
  CODIGO_DE_ESTADO,
  etiquetaDeFlecha,
  grafoDesdeVersion,
  INICIO,
  nuevaClave,
  ordenar,
  peticionDesdeGrafo,
  POSICION_INICIO,
  type Aviso,
  type FlechaDelGrafo,
  type Grafo,
  type PasoDelGrafo,
} from './grafoDelFlujo'

const COLOR = {
  Completada: 'var(--color-green-600)',
  Fallida: 'var(--color-red-600)',
  inicio: 'var(--color-gray-500)',
} as const

const nombreDeTipo: Record<TipoPaso, string> = {
  Rpa: 'Servicio',
  RevisionHumana: 'Revisión humana',
  Api: 'Llamada a API',
  Agente: 'Agente',
  Espera: 'Espera',
  Interno: 'Interno',
  Decision: 'Decisión',
}

const otrosTipos: TipoPaso[] = ['RevisionHumana', 'Api', 'Agente', 'Espera', 'Interno']

const configuracionHint: Partial<Record<TipoPaso, string>> = {
  Agente: 'Libre — lo interpreta el propio agente.',
  Api: '{"url": "...", "method": "GET", "headersTemplate": {...}}',
  Espera: '{"duracionMinutos": 60} o {"condicionReanudacion": "..."}',
  RevisionHumana: '{"instrucciones": "..."}',
  Interno: 'Libre — lo interpreta el propio ejecutor.',
}

const TIPO_ARRASTRADO = 'application/x-viriato-paso'

// Never zoom in past life size: with one or two boxes, fitting the view would blow them up.
const AJUSTE = { padding: 0.2, maxZoom: 1, duration: 300 }

type Seleccion = { tipo: 'paso'; clave: string } | { tipo: 'flecha'; id: string } | null

// ---------------------------------------------------------------- canvas pieces

type PasoNodeData = { paso: PasoDelGrafo; subtitulo: string; aviso: string | null; editable: boolean }
type PasoNode = Node<PasoNodeData, 'paso'>
type InicioNode = Node<{ aviso: string | null }, 'inicio'>

function PasoNodo({ data, selected }: NodeProps<PasoNode>) {
  const { paso, subtitulo, aviso, editable } = data
  return (
    <div
      title={aviso ?? undefined}
      className={`w-56 rounded-xl border bg-surface px-3 py-2 shadow-sm transition-colors ${
        selected ? 'border-indigo-500 ring-2 ring-indigo-500/30' : aviso ? 'border-amber-400 ring-2 ring-amber-400/25' : 'border-gray-300'
      }`}
    >
      <Handle type="target" position={Position.Left} id="in" className="!h-3 !w-3 !border-2 !border-surface !bg-gray-400" />
      <p className="truncate text-sm font-semibold text-gray-900">{paso.nombre || 'Sin nombre'}</p>
      <p className="truncate text-xs text-gray-500">{subtitulo}</p>
      {aviso && <p className="mt-1 truncate text-[11px] text-amber-700">⚠ {aviso}</p>}
      <Handle
        type="source"
        position={Position.Right}
        id="ok"
        isConnectable={editable}
        title="Arrastra para la flecha de «terminó Completada»"
        className="!h-3.5 !w-3.5 !border-2 !border-surface"
        style={{ background: COLOR.Completada }}
      />
      <Handle
        type="source"
        position={Position.Bottom}
        id="ko"
        isConnectable={editable}
        title="Arrastra para la flecha de «terminó Fallida»"
        className="!h-3.5 !w-3.5 !border-2 !border-surface"
        style={{ background: COLOR.Fallida }}
      />
    </div>
  )
}

function InicioNodo({ data, selected }: NodeProps<InicioNode>) {
  return (
    <div
      title={data.aviso ?? undefined}
      className={`rounded-full bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow-sm ${
        selected ? 'ring-2 ring-indigo-500/40' : data.aviso ? 'ring-2 ring-amber-400/50' : ''
      }`}
    >
      Inicio
      <Handle type="source" position={Position.Right} id="inicio" className="!h-3.5 !w-3.5 !border-2 !border-surface !bg-gray-500" />
    </div>
  )
}

type FlechaEdgeData = { flecha: FlechaDelGrafo; indice: number; problema: boolean; seleccionar: (id: string) => void }
type FlechaEdge = Edge<FlechaEdgeData, 'flecha'>

function FlechaArista({ id, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, markerEnd, selected, data }: EdgeProps<FlechaEdge>) {
  const [path, labelX, labelY] = getBezierPath({ sourceX, sourceY, sourcePosition, targetX, targetY, targetPosition })
  const flecha = data!.flecha
  const color = flecha.origen === INICIO ? COLOR.inicio : COLOR[flecha.resultado ?? 'Completada']
  // Several arrows between the same two steps share a path: their labels are stacked so each can be read and picked.
  const desplazamiento = data!.indice * 26
  return (
    <>
      <BaseEdge id={id} path={path} markerEnd={markerEnd} style={{ stroke: color, strokeWidth: selected ? 3 : 2 }} />
      {flecha.origen !== INICIO && (
        <EdgeLabelRenderer>
          <button
            type="button"
            onClick={() => data!.seleccionar(id)}
            className={`nodrag nopan absolute rounded-full border px-2 py-0.5 font-mono text-[11px] shadow-sm ${
              data!.problema ? 'border-amber-400 bg-amber-50 text-amber-800' : selected ? 'border-indigo-500 bg-surface text-gray-900' : 'border-gray-300 bg-surface text-gray-700'
            }`}
            style={{ transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY + desplazamiento}px)`, pointerEvents: 'all' }}
          >
            <span style={{ color }}>{flecha.resultado === 'Fallida' ? '✗' : '✓'}</span> {etiquetaDeFlecha(flecha)}
          </button>
        </EdgeLabelRenderer>
      )}
    </>
  )
}

const nodeTypes = { paso: PasoNodo, inicio: InicioNodo }
const edgeTypes = { flecha: FlechaArista }

// ---------------------------------------------------------------- editor

export function FlujoTransicionesEditor({ flujoId, versionId, onClose }: { flujoId: string; versionId: string; onClose: () => void }) {
  const versionQuery = useQuery({
    queryKey: [`flujo-${flujoId}-version-${versionId}`],
    queryFn: () => flujosApi.getFlujoVersion(flujoId, versionId),
  })

  if (versionQuery.isLoading) return <p className="p-4 text-sm text-gray-500">Cargando…</p>
  const version = versionQuery.data
  if (!version) return <p className="p-4 text-sm text-red-600">No se pudo cargar la versión.</p>

  // A version from before arrows: a draft is carried over to arrows (if it can be); a published or archived one is shown as it was
  // built, a list of steps.
  const convertible = grafoDesdeVersion(version)
  if (convertible === null || (version.transiciones.length === 0 && version.estado !== 'Borrador')) {
    return <FlujoPasosEditor flujoId={flujoId} versionId={versionId} onClose={onClose} />
  }

  return (
    <ReactFlowProvider>
      <Lienzo key={version.id} flujoId={flujoId} version={version} onClose={onClose} />
    </ReactFlowProvider>
  )
}

function Lienzo({ flujoId, version, onClose }: { flujoId: string; version: flujosApi.FlujoVersionDetailDto; onClose: () => void }) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const { screenToFlowPosition, fitView } = useReactFlow()
  const editable = version.estado === 'Borrador'

  const [grafo, setGrafo] = useState<Grafo>(() => grafoDesdeVersion(version)!)
  const [seleccion, setSeleccion] = useState<Seleccion>(null)
  const [cambios, setCambios] = useState(version.transiciones.length === 0 && version.pasos.length > 0)
  const [confirmarSalida, setConfirmarSalida] = useState(false)

  const serviciosQuery = useQuery({ queryKey: ['servicios-all'], queryFn: () => rpaApi.listServicios() })
  const agentesQuery = useQuery({ queryKey: ['agentes-all'], queryFn: () => flujosApi.listAgentes() })
  const estadosQuery = useQuery({ queryKey: [`flujo-${flujoId}-estados`], queryFn: () => flujosApi.listFlujoEstados(flujoId) })

  const servicios = useMemo(() => serviciosQuery.data?.items ?? [], [serviciosQuery.data])
  const agentes = agentesQuery.data?.items ?? []
  const estados = (estadosQuery.data ?? []).filter((e) => e.activo)
  const nombreDeServicio = useMemo(() => new Map(servicios.map((s) => [s.id, s.nombre])), [servicios])

  const avisos = useMemo(() => calcularAvisos(grafo), [grafo])
  const avisoDe = useMemo(() => {
    const mapa = new Map<string, string>()
    for (const a of avisos) if (a.objetivo && !mapa.has(a.objetivo)) mapa.set(a.objetivo, a.texto)
    return mapa
  }, [avisos])
  const bloqueantes = avisos.filter((a) => a.bloquea)

  function cambiar(actualizar: (g: Grafo) => Grafo) {
    if (!editable) return
    setGrafo(actualizar)
    setCambios(true)
  }

  // ---- canvas model
  const nodes: Node[] = useMemo(
    () => [
      {
        id: INICIO,
        type: 'inicio',
        position: POSICION_INICIO,
        data: { aviso: avisoDe.get(INICIO) ?? null },
        draggable: false,
        deletable: false,
        selected: false,
      } satisfies InicioNode,
      ...grafo.pasos.map(
        (p) =>
          ({
            id: p.clave,
            type: 'paso',
            position: { x: p.x, y: p.y },
            data: {
              paso: p,
              subtitulo: p.tipoPaso === 'Rpa' ? (p.servicioId ? (nombreDeServicio.get(p.servicioId) ?? 'Servicio') : 'Sin servicio') : nombreDeTipo[p.tipoPaso],
              aviso: avisoDe.get(p.clave) ?? null,
              editable,
            },
            selected: seleccion?.tipo === 'paso' && seleccion.clave === p.clave,
            deletable: editable,
          }) satisfies PasoNode,
      ),
    ],
    [grafo.pasos, avisoDe, nombreDeServicio, seleccion, editable],
  )

  const edges: Edge[] = useMemo(() => {
    const vistos = new Map<string, number>()
    return grafo.flechas.map((f) => {
      const par = `${f.origen}>${f.destino}>${f.resultado}`
      const indice = vistos.get(par) ?? 0
      vistos.set(par, indice + 1)
      const color = f.origen === INICIO ? COLOR.inicio : COLOR[f.resultado ?? 'Completada']
      return {
        id: f.id,
        type: 'flecha',
        source: f.origen,
        sourceHandle: f.origen === INICIO ? 'inicio' : f.resultado === 'Fallida' ? 'ko' : 'ok',
        target: f.destino,
        targetHandle: 'in',
        markerEnd: { type: MarkerType.ArrowClosed, color, width: 18, height: 18 },
        selected: seleccion?.tipo === 'flecha' && seleccion.id === f.id,
        deletable: editable,
        data: { flecha: f, indice, problema: avisoDe.has(f.id), seleccionar: (id: string) => setSeleccion({ tipo: 'flecha', id }) },
      } satisfies FlechaEdge
    })
  }, [grafo.flechas, seleccion, editable, avisoDe])

  function onNodesChange(changes: NodeChange[]) {
    const movidos = new Map<string, { x: number; y: number }>()
    const quitados = new Set<string>()
    for (const c of changes) {
      if (c.type === 'position' && c.position) movidos.set(c.id, c.position)
      if (c.type === 'remove' && c.id !== INICIO) quitados.add(c.id)
    }
    if (movidos.size === 0 && quitados.size === 0) return
    cambiar((g) => ({
      pasos: g.pasos.filter((p) => !quitados.has(p.clave)).map((p) => (movidos.has(p.clave) ? { ...p, ...movidos.get(p.clave)! } : p)),
      flechas: g.flechas.filter((f) => !quitados.has(f.origen) && !quitados.has(f.destino)),
    }))
    if (seleccion?.tipo === 'paso' && quitados.has(seleccion.clave)) setSeleccion(null)
  }

  function onEdgesChange(changes: EdgeChange[]) {
    const quitadas = new Set(changes.filter((c) => c.type === 'remove').map((c) => c.id))
    if (quitadas.size === 0) return
    cambiar((g) => ({ ...g, flechas: g.flechas.filter((f) => !quitadas.has(f.id)) }))
    if (seleccion?.tipo === 'flecha' && quitadas.has(seleccion.id)) setSeleccion(null)
  }

  function onConnect(conexion: Connection) {
    if (!conexion.source || !conexion.target || conexion.target === INICIO) return
    const id = nuevaClave('flecha')
    const desdeInicio = conexion.source === INICIO
    cambiar((g) => ({
      ...g,
      // There is a single start arrow: drawing a new one moves it.
      flechas: [
        ...g.flechas.filter((f) => !(desdeInicio && f.origen === INICIO)),
        {
          id,
          origen: conexion.source,
          destino: conexion.target,
          resultado: desdeInicio ? null : conexion.sourceHandle === 'ko' ? 'Fallida' : 'Completada',
          estado: null,
        },
      ],
    }))
    if (!desdeInicio) setSeleccion({ tipo: 'flecha', id })
  }

  // ---- adding steps
  function siguientePosicion() {
    const x = Math.max(0, ...grafo.pasos.map((p) => p.x)) + ANCHO_COLUMNA
    return { x, y: 0 }
  }

  function anadirPaso(tipoPaso: TipoPaso, servicioId: string | null, posicion = siguientePosicion()) {
    const clave = nuevaClave()
    const nombre = servicioId ? (nombreDeServicio.get(servicioId) ?? 'Servicio') : nombreDeTipo[tipoPaso]
    cambiar((g) => {
      const pasos = [
        ...g.pasos,
        { clave, nombre, tipoPaso, servicioId, agenteDefinicionId: null, configuracionJson: null, maximoDeVisitas: null, x: posicion.x, y: posicion.y },
      ]
      // The first step of an empty flow is where Casos start.
      const flechas = g.pasos.length === 0 ? [...g.flechas, { id: nuevaClave('flecha'), origen: INICIO, destino: clave, resultado: null, estado: null }] : g.flechas
      return { pasos, flechas }
    })
    setSeleccion({ tipo: 'paso', clave })
    // A step added from the palette lands beside the others, possibly out of view: bring it in.
    window.setTimeout(() => fitView(AJUSTE), 0)
  }

  function onDragStart(e: DragEvent, tipoPaso: TipoPaso, servicioId: string | null) {
    e.dataTransfer.setData(TIPO_ARRASTRADO, JSON.stringify({ tipoPaso, servicioId }))
    e.dataTransfer.effectAllowed = 'move'
  }

  function onDrop(e: DragEvent) {
    const datos = e.dataTransfer.getData(TIPO_ARRASTRADO)
    if (!datos || !editable) return
    e.preventDefault()
    const { tipoPaso, servicioId } = JSON.parse(datos) as { tipoPaso: TipoPaso; servicioId: string | null }
    const punto = screenToFlowPosition({ x: e.clientX, y: e.clientY })
    anadirPaso(tipoPaso, servicioId, { x: punto.x - 112, y: punto.y - 24 })
  }

  // ---- edits from the panel
  const actualizarPaso = (clave: string, patch: Partial<PasoDelGrafo>) =>
    cambiar((g) => ({ ...g, pasos: g.pasos.map((p) => (p.clave === clave ? { ...p, ...patch } : p)) }))
  const actualizarFlecha = (id: string, patch: Partial<FlechaDelGrafo>) =>
    cambiar((g) => ({ ...g, flechas: g.flechas.map((f) => (f.id === id ? { ...f, ...patch } : f)) }))
  const quitarPaso = (clave: string) => {
    onNodesChange([{ type: 'remove', id: clave }])
  }
  const quitarFlecha = (id: string) => onEdgesChange([{ type: 'remove', id }])

  // ---- saving
  const saveMutation = useMutation({
    mutationFn: () => flujosApi.replacePasos(flujoId, version.id, peticionDesdeGrafo(grafo)),
    onSuccess: (guardada) => {
      queryClient.setQueryData([`flujo-${flujoId}-version-${version.id}`], guardada)
      queryClient.invalidateQueries({ queryKey: [`flujo-${flujoId}-versiones`] })
      // Saving creates the states the arrows name that the process did not have.
      queryClient.invalidateQueries({ queryKey: [`flujo-${flujoId}-estados`] })
      setGrafo(grafoDesdeVersion(guardada)!)
      setSeleccion(null)
      setCambios(false)
      showToast('success', 'Flujo guardado.')
    },
    onError: (err) => showToast('error', mensajeDeError(err, 'No se pudo guardar el flujo.')),
  })

  useEffect(() => {
    if (!cambios) return
    const avisar = (e: BeforeUnloadEvent) => e.preventDefault()
    window.addEventListener('beforeunload', avisar)
    return () => window.removeEventListener('beforeunload', avisar)
  }, [cambios])

  const pasoSeleccionado = seleccion?.tipo === 'paso' ? grafo.pasos.find((p) => p.clave === seleccion.clave) : undefined
  const flechaSeleccionada = seleccion?.tipo === 'flecha' ? grafo.flechas.find((f) => f.id === seleccion.id) : undefined
  const usados = new Set(grafo.pasos.map((p) => p.servicioId).filter(Boolean))
  const nombreDePaso = (clave: string) => (clave === INICIO ? 'Inicio' : (grafo.pasos.find((p) => p.clave === clave)?.nombre ?? '?'))

  return (
    <Card className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h2 className="text-lg font-semibold text-gray-900">
            Flujo — v{version.numeroVersion}{' '}
            {!editable && <span className="text-sm font-normal text-gray-500">(solo lectura — {version.estado})</span>}
          </h2>
          <p className="text-sm text-gray-500">
            {editable
              ? 'Arrastra servicios al lienzo y únelos: el asa verde es «terminó Completada», la roja «terminó Fallida». Pulsa una flecha para elegir el estado de negocio.'
              : 'Cada flecha: cuando el servicio termina así y el caso está en ese estado, el caso pasa al siguiente.'}
          </p>
        </div>
        <div className="flex items-center gap-2">
          {editable && (
            <Button
              variant="secondary"
              size="sm"
              onClick={() => {
                cambiar(ordenar)
                window.setTimeout(() => fitView(AJUSTE), 0)
              }}
            >
              Ordenar
            </Button>
          )}
          <Button variant="ghost" size="sm" onClick={() => (cambios && editable ? setConfirmarSalida(true) : onClose())}>
            Volver
          </Button>
          {editable && (
            <Button
              size="sm"
              disabled={!cambios || bloqueantes.length > 0 || saveMutation.isPending}
              title={bloqueantes[0]?.texto}
              onClick={() => saveMutation.mutate()}
            >
              {saveMutation.isPending ? 'Guardando…' : 'Guardar'}
            </Button>
          )}
        </div>
      </div>

      <div className="grid gap-3 lg:grid-cols-[13rem_minmax(0,1fr)_18rem]">
        {/* Palette */}
        {editable ? (
          <aside className="order-2 flex flex-col gap-3 lg:order-1" aria-label="Pasos para añadir">
            <div>
              <h3 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-gray-500">Servicios</h3>
              <ul className="flex max-h-72 flex-col gap-1 overflow-y-auto lg:max-h-[28rem]">
                {servicios
                  .filter((s) => s.activo)
                  .map((s) => {
                    const usado = usados.has(s.id)
                    return (
                      <li key={s.id}>
                        <button
                          type="button"
                          draggable={!usado}
                          disabled={usado}
                          onDragStart={(e) => onDragStart(e, 'Rpa', s.id)}
                          onClick={() => anadirPaso('Rpa', s.id)}
                          title={usado ? 'Ya está en el flujo' : 'Arrástralo al lienzo o púlsalo para añadirlo'}
                          className="w-full cursor-grab truncate rounded-lg border border-gray-200 bg-surface px-2.5 py-1.5 text-left text-sm text-gray-800 enabled:hover:border-indigo-400 disabled:cursor-default disabled:opacity-40"
                        >
                          {s.nombre}
                        </button>
                      </li>
                    )
                  })}
                {servicios.length === 0 && <li className="text-xs text-gray-500">No hay servicios. Créalos en RPA → Servicios.</li>}
              </ul>
            </div>
            <div>
              <h3 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-gray-500">Otros pasos</h3>
              <ul className="flex flex-col gap-1">
                {otrosTipos.map((tipo) => (
                  <li key={tipo}>
                    <button
                      type="button"
                      draggable
                      onDragStart={(e) => onDragStart(e, tipo, null)}
                      onClick={() => anadirPaso(tipo, null)}
                      className="w-full cursor-grab rounded-lg border border-dashed border-gray-300 px-2.5 py-1.5 text-left text-sm text-gray-700 hover:border-indigo-400"
                    >
                      {nombreDeTipo[tipo]}
                    </button>
                  </li>
                ))}
              </ul>
            </div>
          </aside>
        ) : (
          <div className="hidden lg:block" />
        )}

        {/* Canvas */}
        <div
          className="order-1 h-[60vh] min-h-[420px] overflow-hidden rounded-xl border border-gray-200 bg-gray-50 lg:order-2 lg:h-[calc(100dvh-18rem)]"
          onDragOver={(e) => {
            if (!editable) return
            e.preventDefault()
            e.dataTransfer.dropEffect = 'move'
          }}
          onDrop={onDrop}
          // React Flow's own controls and selection box, in the app's colours (which the dark theme redefines).
          style={
            {
              '--xy-controls-button-background-color': 'var(--color-surface)',
              '--xy-controls-button-background-color-hover': 'var(--color-gray-100)',
              '--xy-controls-button-color': 'var(--color-gray-700)',
              '--xy-controls-button-color-hover': 'var(--color-gray-900)',
              '--xy-controls-button-border-color': 'var(--color-gray-200)',
              '--xy-edge-label-background-color': 'var(--color-surface)',
            } as CSSProperties
          }
        >
          <ReactFlow
            nodes={nodes}
            edges={edges}
            nodeTypes={nodeTypes}
            edgeTypes={edgeTypes}
            onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            onConnect={onConnect}
            onNodeClick={(_, node) => setSeleccion(node.id === INICIO ? null : { tipo: 'paso', clave: node.id })}
            onEdgeClick={(_, edge) => setSeleccion({ tipo: 'flecha', id: edge.id })}
            onPaneClick={() => setSeleccion(null)}
            nodesDraggable={editable}
            nodesConnectable={editable}
            elementsSelectable
            deleteKeyCode={editable ? ['Backspace', 'Delete'] : null}
            isValidConnection={(c) => c.target !== INICIO}
            fitView
            fitViewOptions={AJUSTE}
            minZoom={0.3}
            proOptions={{ hideAttribution: true }}
          >
            <Background gap={20} color="var(--color-gray-300)" />
            <Controls showInteractive={false} />
          </ReactFlow>
        </div>

        {/* Properties */}
        <aside className="order-3 flex flex-col gap-3 rounded-xl border border-gray-200 p-3" aria-label="Propiedades">
          {pasoSeleccionado ? (
            <PanelDePaso
              paso={pasoSeleccionado}
              editable={editable}
              servicio={pasoSeleccionado.servicioId ? nombreDeServicio.get(pasoSeleccionado.servicioId) : undefined}
              agentes={agentes}
              aviso={avisoDe.get(pasoSeleccionado.clave)}
              onChange={(patch) => actualizarPaso(pasoSeleccionado.clave, patch)}
              onRemove={() => quitarPaso(pasoSeleccionado.clave)}
            />
          ) : flechaSeleccionada ? (
            <PanelDeFlecha
              flecha={flechaSeleccionada}
              desde={nombreDePaso(flechaSeleccionada.origen)}
              hacia={nombreDePaso(flechaSeleccionada.destino)}
              estados={estados}
              editable={editable}
              aviso={avisoDe.get(flechaSeleccionada.id)}
              onChange={(patch) => actualizarFlecha(flechaSeleccionada.id, patch)}
              onRemove={() => quitarFlecha(flechaSeleccionada.id)}
            />
          ) : (
            <Resumen grafo={grafo} avisos={avisos} nombreDePaso={nombreDePaso} onSeleccionar={setSeleccion} />
          )}
        </aside>
      </div>

      <ConfirmDialog
        open={confirmarSalida}
        title="Salir sin guardar"
        message="Hay cambios en el flujo que no se han guardado. Si sales, se pierden."
        confirmLabel="Salir sin guardar"
        onConfirm={onClose}
        onCancel={() => setConfirmarSalida(false)}
      />
    </Card>
  )
}

// ---------------------------------------------------------------- side panels

function Campo({ etiqueta, children }: { etiqueta: string; children: ReactNode }) {
  return (
    <label className="flex flex-col gap-1 text-sm">
      <span className="font-medium text-gray-700">{etiqueta}</span>
      {children}
    </label>
  )
}

function PanelDePaso({
  paso,
  editable,
  servicio,
  agentes,
  aviso,
  onChange,
  onRemove,
}: {
  paso: PasoDelGrafo
  editable: boolean
  servicio: string | undefined
  agentes: flujosApi.AgenteDefinicionDto[]
  aviso: string | undefined
  onChange: (patch: Partial<PasoDelGrafo>) => void
  onRemove: () => void
}) {
  return (
    <>
      <h3 className="text-sm font-semibold text-gray-900">{paso.tipoPaso === 'Rpa' ? 'Servicio' : nombreDeTipo[paso.tipoPaso]}</h3>
      {aviso && <p className="rounded-lg bg-amber-50 px-2.5 py-1.5 text-xs text-amber-800">⚠ {aviso}</p>}
      <Campo etiqueta="Nombre del paso">
        <input
          className="field min-h-9 px-3 py-1.5 text-sm disabled:bg-gray-50"
          value={paso.nombre}
          disabled={!editable}
          onChange={(e) => onChange({ nombre: e.target.value })}
        />
      </Campo>
      {paso.tipoPaso === 'Rpa' && (
        <p className="text-sm text-gray-600">
          <span className="font-medium text-gray-700">Servicio:</span> {servicio ?? 'sin servicio'}
          <span className="mt-0.5 block text-xs text-gray-500">Para usar otro servicio, borra este paso y arrastra el otro.</span>
        </p>
      )}
      {paso.tipoPaso === 'Agente' && (
        <Campo etiqueta="Agente">
          <select
            className="field min-h-9 px-3 py-1.5 text-sm disabled:bg-gray-50"
            value={paso.agenteDefinicionId ?? ''}
            disabled={!editable}
            onChange={(e) => onChange({ agenteDefinicionId: e.target.value || null })}
          >
            <option value="">Seleccionar agente…</option>
            {agentes.map((a) => (
              <option key={a.id} value={a.id}>
                {a.nombre}
              </option>
            ))}
          </select>
        </Campo>
      )}
      {paso.tipoPaso !== 'Rpa' && (
        <Campo etiqueta="Configuración">
          <textarea
            className="field min-h-9 px-3 py-1.5 font-mono text-xs disabled:bg-gray-50"
            rows={3}
            placeholder={configuracionHint[paso.tipoPaso]}
            value={paso.configuracionJson ?? ''}
            disabled={!editable}
            onChange={(e) => onChange({ configuracionJson: e.target.value })}
          />
        </Campo>
      )}
      <Campo etiqueta="Máximo de vueltas">
        <input
          type="number"
          min={1}
          className="field min-h-9 px-3 py-1.5 text-sm disabled:bg-gray-50"
          placeholder="10"
          value={paso.maximoDeVisitas ?? ''}
          disabled={!editable}
          onChange={(e) => onChange({ maximoDeVisitas: e.target.value ? Math.max(1, Number(e.target.value)) : null })}
        />
        <span className="text-xs text-gray-500">Veces que un caso puede pasar por aquí antes de darlo por fallido (si una flecha vuelve atrás).</span>
      </Campo>
      {editable && (
        <Button variant="danger" size="sm" className="self-start" onClick={onRemove}>
          Quitar paso
        </Button>
      )}
    </>
  )
}

function PanelDeFlecha({
  flecha,
  desde,
  hacia,
  estados,
  editable,
  aviso,
  onChange,
  onRemove,
}: {
  flecha: FlechaDelGrafo
  desde: string
  hacia: string
  estados: flujosApi.FlujoEstadoDefDto[]
  editable: boolean
  aviso: string | undefined
  onChange: (patch: Partial<FlechaDelGrafo>) => void
  onRemove: () => void
}) {
  const esInicio = flecha.origen === INICIO
  const estado = flecha.estado ?? ''
  const estadoNuevo = estado.trim() !== '' && CODIGO_DE_ESTADO.test(estado.trim()) && !estados.some((e) => e.codigo === estado.trim())
  return (
    <>
      <h3 className="text-sm font-semibold text-gray-900">Flecha</h3>
      <p className="text-sm text-gray-600">
        <span className="font-medium text-gray-800">{desde}</span> → <span className="font-medium text-gray-800">{hacia}</span>
      </p>
      {aviso && <p className="rounded-lg bg-amber-50 px-2.5 py-1.5 text-xs text-amber-800">⚠ {aviso}</p>}
      {esInicio ? (
        <p className="text-sm text-gray-600">Los casos nuevos empiezan en «{hacia}». Para cambiarlo, arrastra desde Inicio a otro paso.</p>
      ) : (
        <>
          <fieldset className="flex flex-col gap-1 text-sm" disabled={!editable}>
            <legend className="mb-1 font-medium text-gray-700">Cuando termina</legend>
            <div className="flex gap-2">
              {(['Completada', 'Fallida'] as ResultadoEjecucion[]).map((r) => (
                <button
                  key={r}
                  type="button"
                  onClick={() => onChange({ resultado: r })}
                  aria-pressed={flecha.resultado === r}
                  className={`flex-1 rounded-lg border px-2 py-1.5 text-sm disabled:opacity-60 ${
                    flecha.resultado === r ? 'border-transparent text-white' : 'border-gray-300 text-gray-700'
                  }`}
                  style={flecha.resultado === r ? { background: COLOR[r] } : undefined}
                >
                  {r === 'Completada' ? '✓ Completada' : '✗ Fallida'}
                </button>
              ))}
            </div>
          </fieldset>
          <Campo etiqueta="Con el caso en el estado">
            <input
              className="field min-h-9 px-3 py-1.5 font-mono text-sm disabled:bg-gray-50"
              list="estados-del-proceso"
              placeholder="Cualquier estado"
              value={estado}
              disabled={!editable}
              onChange={(e) => onChange({ estado: e.target.value.toUpperCase() || null })}
            />
            <datalist id="estados-del-proceso">
              {estados.map((e) => (
                <option key={e.id} value={e.codigo}>
                  {e.display}
                </option>
              ))}
            </datalist>
            <span className="text-xs text-gray-500">
              {estadoNuevo
                ? 'Este estado no existe todavía: se creará al guardar.'
                : 'Vacío: vale cualquier estado. Si otra flecha nombra el estado exacto del caso, gana esa.'}
            </span>
          </Campo>
        </>
      )}
      {editable && (
        <Button variant="danger" size="sm" className="self-start" onClick={onRemove}>
          Quitar flecha
        </Button>
      )}
    </>
  )
}

function Resumen({
  grafo,
  avisos,
  nombreDePaso,
  onSeleccionar,
}: {
  grafo: Grafo
  avisos: Aviso[]
  nombreDePaso: (clave: string) => string
  onSeleccionar: (s: Seleccion) => void
}) {
  const seleccionarObjetivo = (objetivo: string | null) => {
    if (!objetivo || objetivo === INICIO) return
    onSeleccionar(grafo.flechas.some((f) => f.id === objetivo) ? { tipo: 'flecha', id: objetivo } : { tipo: 'paso', clave: objetivo })
  }
  return (
    <>
      <h3 className="text-sm font-semibold text-gray-900">Reglas</h3>
      {grafo.flechas.length === 0 ? (
        <p className="text-sm text-gray-500">Todavía no hay flechas.</p>
      ) : (
        <ul className="flex flex-col gap-1.5 text-xs text-gray-700">
          {grafo.flechas.map((f) => (
            <li key={f.id}>
              <button type="button" className="text-left hover:text-indigo-600" onClick={() => onSeleccionar(f.origen === INICIO ? null : { tipo: 'flecha', id: f.id })}>
                {f.origen === INICIO ? (
                  <>
                    <b>Inicio</b> → {nombreDePaso(f.destino)}
                  </>
                ) : (
                  <>
                    <b>{nombreDePaso(f.origen)}</b> · <span style={{ color: COLOR[f.resultado ?? 'Completada'] }}>{f.resultado}</span> ·{' '}
                    <span className="font-mono">{etiquetaDeFlecha(f)}</span> → {nombreDePaso(f.destino)}
                  </>
                )}
              </button>
            </li>
          ))}
        </ul>
      )}
      <p className="text-xs text-gray-500">Si al terminar un servicio no aplica ninguna flecha, el caso se cierra con el resultado de esa ejecución.</p>
      {avisos.length > 0 && (
        <>
          <h3 className="mt-1 text-sm font-semibold text-gray-900">Avisos</h3>
          <ul className="flex flex-col gap-1 text-xs">
            {avisos.map((a, i) => (
              <li key={i}>
                <button
                  type="button"
                  className={`text-left ${a.bloquea ? 'text-red-700' : 'text-amber-700'} hover:underline`}
                  onClick={() => seleccionarObjetivo(a.objetivo)}
                >
                  {a.bloquea ? '✗' : '⚠'} {a.texto}
                </button>
              </li>
            ))}
          </ul>
        </>
      )}
    </>
  )
}
