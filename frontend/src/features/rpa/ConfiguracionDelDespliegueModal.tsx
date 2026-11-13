import { useMutation, useQueryClient } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { useState, type FormEvent, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Button } from '../../components/ui/Button'
import { DatoRotulado } from '../../components/ui/DatoRotulado'
import { Input } from '../../components/ui/Input'
import { Modal } from '../../components/ui/Modal'
import { Switch } from '../../components/ui/Switch'
import { Tabs } from '../../components/ui/Tabs'
import { TarjetasDeOpciones } from '../../components/ui/TarjetasDeOpciones'
import { mensajeDeError } from '../../lib/apiClient'
import { tabPanelVariants } from '../../lib/motion/variants'
import { useToast } from '../../lib/toast/useToast'
import * as rpaApi from './api'
import type { DespliegueDto } from './api'
import { useVersionesDelServicio } from './useVersionesDelServicio'
import { calidadesDeVista, opcionesDeGrabacion, type CalidadVistaEnDirecto, type GrabacionDeVideo } from './pantallaDelRobot'
import { numeroOpcional } from './versionesDelRobot'

export type ApartadoDelDespliegue = 'general' | 'robot' | 'replicas' | 'pantalla' | 'clave'

const apartados: { value: ApartadoDelDespliegue; label: string }[] = [
  { value: 'general', label: 'General' },
  { value: 'robot', label: 'Robot' },
  { value: 'replicas', label: 'Réplicas' },
  { value: 'pantalla', label: 'Vista en directo' },
  { value: 'clave', label: 'Clave' },
]

interface Props {
  /** The live row, from the list: it refreshes after every save. */
  despliegue: DespliegueDto | null
  apartado: ApartadoDelDespliegue
  alCambiarApartado: (apartado: ApartadoDelDespliegue) => void
  flujos: readonly { id: string; nombre: string }[]
  nombreFlujo: (id: string) => string
  alRegenerarClave: (despliegue: DespliegueDto) => void
  regenerando: boolean
  onClose: () => void
}

/** Everything about one despliegue in one place, split by what it is about, each part saved on its own. */
export function ConfiguracionDelDespliegueModal({
  despliegue,
  apartado,
  alCambiarApartado,
  flujos,
  nombreFlujo,
  alRegenerarClave,
  regenerando,
  onClose,
}: Props) {
  return (
    <Modal
      open={despliegue !== null}
      size="lg"
      title={despliegue ? `${despliegue.servicioNombre} en ${despliegue.equipoNombre}` : 'Despliegue'}
      onClose={onClose}
      footer={
        <div className="flex justify-end">
          <Button type="button" variant="ghost" onClick={onClose}>
            Cerrar
          </Button>
        </div>
      }
    >
      {despliegue && (
        <div className="flex flex-col gap-4">
          <Tabs tabs={apartados} active={apartado} onChange={alCambiarApartado} />
          <AnimatePresence mode="wait" initial={false}>
            <motion.div key={apartado} variants={tabPanelVariants} initial="initial" animate="animate" exit="exit" className="min-h-[18rem]">
              {apartado === 'general' && <General key={despliegue.id} despliegue={despliegue} flujos={flujos} nombreFlujo={nombreFlujo} />}
              {apartado === 'robot' && <Robot key={despliegue.id} despliegue={despliegue} />}
              {apartado === 'replicas' && <Replicas key={despliegue.id} despliegue={despliegue} />}
              {apartado === 'pantalla' && <Pantalla key={despliegue.id} despliegue={despliegue} />}
              {apartado === 'clave' && <Clave despliegue={despliegue} regenerando={regenerando} alRegenerar={() => alRegenerarClave(despliegue)} />}
            </motion.div>
          </AnimatePresence>
        </div>
      )}
    </Modal>
  )
}

function useGuardarDespliegue(mensaje: string) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  return {
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['despliegues'] })
      showToast('success', mensaje)
    },
    onError: (err: unknown) => showToast('error', mensajeDeError(err, 'No se pudo guardar.')),
  }
}

/** A part of the form: a short line on what it is for, its fields, and its own Guardar. */
function Apartado({
  descripcion,
  children,
  alGuardar,
  guardando,
  sinCambios,
  invalido = false,
}: {
  descripcion: ReactNode
  children: ReactNode
  alGuardar: () => void
  guardando: boolean
  sinCambios: boolean
  invalido?: boolean
}) {
  function enviar(event: FormEvent) {
    event.preventDefault()
    if (!invalido && !sinCambios) alGuardar()
  }
  return (
    <form onSubmit={enviar} className="flex flex-col gap-4">
      <p className="text-sm text-gray-600">{descripcion}</p>
      {children}
      <div className="flex justify-end">
        <Button type="submit" disabled={guardando || sinCambios || invalido}>
          {guardando ? 'Guardando…' : 'Guardar'}
        </Button>
      </div>
    </form>
  )
}

function General({
  despliegue,
  flujos,
  nombreFlujo,
}: {
  despliegue: DespliegueDto
  flujos: readonly { id: string; nombre: string }[]
  nombreFlujo: (id: string) => string
}) {
  const [encendido, setEncendido] = useState(despliegue.encendido)
  const [destino, setDestino] = useState(despliegue.flujoDestinoId ?? '')
  const guardar = useMutation({
    mutationFn: () =>
      rpaApi.updateDespliegue(despliegue.id, {
        encendido,
        ...(destino ? { flujoDestinoId: destino } : { quitarFlujoDestino: true }),
      }),
    ...useGuardarDespliegue('Despliegue actualizado.'),
  })
  const sinCambios = encendido === despliegue.encendido && destino === (despliegue.flujoDestinoId ?? '')

  return (
    <Apartado
      descripcion="Qué robot es, dónde corre y para qué proceso trabaja."
      alGuardar={() => guardar.mutate()}
      guardando={guardar.isPending}
      sinCambios={sinCambios}
    >
      <div className="grid gap-4 rounded-xl border border-gray-200 px-4 py-3 sm:grid-cols-3">
        <DatoRotulado rotulo="Equipo">
          <span className="text-sm text-gray-900">{despliegue.equipoNombre}</span>
        </DatoRotulado>
        <DatoRotulado rotulo="Servicio">
          <span className="text-sm text-gray-900">{despliegue.servicioNombre}</span>
        </DatoRotulado>
        <DatoRotulado rotulo="Proceso">
          <span className="text-sm text-gray-900">{nombreFlujo(despliegue.flujoId)}</span>
        </DatoRotulado>
      </div>

      <div className="flex items-start justify-between gap-4">
        <div className="flex flex-col gap-0.5">
          <span className="text-sm font-medium text-gray-700">Encendido</span>
          <span className="text-xs text-gray-500">Apagado, el robot no recibe trabajo ni puede autenticarse para pedirlo.</span>
        </div>
        <Switch checked={encendido} label={encendido ? 'Apagar el despliegue' : 'Encender el despliegue'} onChange={setEncendido} />
      </div>

      <div className="flex flex-col gap-1">
        <label htmlFor="flujo-destino" className="text-sm font-medium text-gray-700">
          Puede crear casos en
        </label>
        <select id="flujo-destino" className="field" value={destino} onChange={(e) => setDestino(e.target.value)}>
          <option value="">Ninguno</option>
          {flujos.map((flujo) => (
            <option key={flujo.id} value={flujo.id}>
              {flujo.nombre}
            </option>
          ))}
        </select>
        <p className="text-xs text-gray-500">Solo para robots que lanzan casos de otro proceso. Sin esto, el robot no puede crear ninguno.</p>
      </div>
    </Apartado>
  )
}

const SIGUE_LA_ULTIMA = ''

/** Whether the launcher next to Viriato runs this despliegue, and with which version of its service's robot. */
function Robot({ despliegue }: { despliegue: DespliegueDto }) {
  const versionesQuery = useVersionesDelServicio(despliegue.servicioId)
  const versiones = versionesQuery.data ?? []
  const ultima = versiones.find((v) => v.ultima)
  const [enElLanzador, setEnElLanzador] = useState(despliegue.enElLanzador)
  const [version, setVersion] = useState(despliegue.versionFijadaId ?? SIGUE_LA_ULTIMA)

  const guardar = useMutation({
    mutationFn: () => rpaApi.guardarRobotDelDespliegue(despliegue.id, { enElLanzador, versionId: version || null }),
    ...useGuardarDespliegue(enElLanzador ? 'Guardado. El lanzador lo aplica en unos segundos.' : 'El lanzador ya no arrancará este robot.'),
  })
  const sinCambios = enElLanzador === despliegue.enElLanzador && version === (despliegue.versionFijadaId ?? SIGUE_LA_ULTIMA)

  return (
    <Apartado
      descripcion={
        <>
          El programa del robot es del servicio: se sube una vez en{' '}
          <Link to="/admin/rpa/servicios" className="font-medium text-indigo-600 hover:underline">
            Servicios
          </Link>{' '}
          y lo comparten todos sus despliegues. Aquí decides si lo arranca el lanzador de Viriato en este equipo, y con qué versión.
        </>
      }
      alGuardar={() => guardar.mutate()}
      guardando={guardar.isPending}
      sinCambios={sinCambios}
    >
      <div className="flex items-start justify-between gap-4">
        <div className="flex flex-col gap-0.5">
          <span className="text-sm font-medium text-gray-700">Lo arranca el lanzador de Viriato</span>
          <span className="text-xs text-gray-500">
            Desactivado, el robot corre en otro sitio y se identifica con la clave del despliegue.
          </span>
        </div>
        <Switch
          checked={enElLanzador}
          label={enElLanzador ? 'Que no lo arranque el lanzador' : 'Que lo arranque el lanzador'}
          onChange={setEnElLanzador}
        />
      </div>

      {enElLanzador && versionesQuery.isSuccess && versiones.length === 0 && (
        <p className="rounded-lg bg-amber-50 px-3 py-2 text-xs text-amber-800">
          {despliegue.servicioNombre} aún no tiene ninguna versión subida: el lanzador lo arrancará en cuanto subas la primera.
        </p>
      )}

      {enElLanzador && versiones.length > 0 && (
        <div className="flex flex-col gap-1">
          <label htmlFor="version-del-robot" className="text-sm font-medium text-gray-700">
            Versión
          </label>
          <select id="version-del-robot" className="field sm:max-w-sm" value={version} onChange={(e) => setVersion(e.target.value)}>
            <option value={SIGUE_LA_ULTIMA}>Siempre la última{ultima ? ` (ahora, la ${ultima.numero})` : ''}</option>
            {versiones.map((v) => (
              <option key={v.id} value={v.id}>
                Fijar en la versión {v.numero} · {new Date(v.createdAt).toLocaleDateString()}
              </option>
            ))}
          </select>
          <p className="text-xs text-gray-500">
            Con «Siempre la última», cada versión que subas llega aquí sola. Fíjalo en una para volver atrás o probar en otro equipo.
          </p>
        </div>
      )}
    </Apartado>
  )
}

function Replicas({ despliegue }: { despliegue: DespliegueDto }) {
  const [replicas, setReplicas] = useState(String(despliegue.replicas))
  const [memoria, setMemoria] = useState(despliegue.memoriaMaximaMb?.toString() ?? '')
  // The same list the robot tab shows (shared cache): the version in use says how much memory it asks for by default.
  const versionesQuery = useVersionesDelServicio(despliegue.servicioId)
  const memoriaDeLaVersion = versionesQuery.data?.find((v) => v.numero === despliegue.versionDeRobotNumero)?.memoriaMaximaMb ?? null

  const numeroDeReplicas = Number(replicas)
  const memoriaMaximaMb = numeroOpcional(memoria)
  const invalido = !Number.isInteger(numeroDeReplicas) || numeroDeReplicas < 0 || numeroDeReplicas > 10 || Number.isNaN(memoriaMaximaMb)
  const sinCambios = numeroDeReplicas === despliegue.replicas && memoriaMaximaMb === despliegue.memoriaMaximaMb

  const guardar = useMutation({
    mutationFn: () => rpaApi.guardarRobotEnElLanzador(despliegue.id, { replicas: numeroDeReplicas, memoriaMaximaMb }),
    ...useGuardarDespliegue(
      numeroDeReplicas === 0 ? 'Aparcado: el lanzador no lo arrancará.' : 'Guardado. El lanzador lo aplica en unos segundos.',
    ),
  })

  return (
    <Apartado
      descripcion="Cuántas copias del robot arranca a la vez el lanzador de Viriato cuando hay trabajo, y cuánto puede ocupar cada una."
      alGuardar={() => guardar.mutate()}
      guardando={guardar.isPending}
      sinCambios={sinCambios}
      invalido={invalido}
    >
      {despliegue.versionDeRobotNumero === null && (
        <p className="rounded-lg bg-amber-50 px-3 py-2 text-xs text-amber-800">
          El lanzador de Viriato no arranca este robot (míralo en la pestaña Robot): hasta entonces, esto no tiene efecto.
        </p>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        <Input
          label="Réplicas"
          type="number"
          inputMode="numeric"
          min={0}
          max={10}
          required
          value={replicas}
          hint="0 lo deja aparcado. Hasta 10."
          onChange={(e) => setReplicas(e.target.value)}
        />
        <Input
          label="Memoria máxima por réplica (MB)"
          type="number"
          inputMode="numeric"
          min={128}
          step={128}
          value={memoria}
          placeholder={memoriaDeLaVersion?.toString() ?? 'Sin tope'}
          hint={`Vacío: ${memoriaDeLaVersion ? `${memoriaDeLaVersion} MB` : 'sin tope'}. La copia que se pase se para.`}
          onChange={(e) => setMemoria(e.target.value)}
        />
      </div>
    </Apartado>
  )
}

function Pantalla({ despliegue }: { despliegue: DespliegueDto }) {
  const [vista, setVista] = useState<CalidadVistaEnDirecto>(despliegue.vistaEnDirecto)
  const [grabacion, setGrabacion] = useState<GrabacionDeVideo>(despliegue.grabacionDeVideo)
  const guardar = useMutation({
    mutationFn: () => rpaApi.updateDespliegue(despliegue.id, { vistaEnDirecto: vista, grabacionDeVideo: grabacion }),
    ...useGuardarDespliegue('Guardado. Quien mira lo nota ya; el robot, en unos segundos.'),
  })
  const sinCambios = vista === despliegue.vistaEnDirecto && grabacion === despliegue.grabacionDeVideo

  return (
    <Apartado
      descripcion="Mirar la pantalla le quita CPU al robot mientras alguien la tiene abierta. Si va lento, bájala o apágala."
      alGuardar={() => guardar.mutate()}
      guardando={guardar.isPending}
      sinCambios={sinCambios}
    >
      <TarjetasDeOpciones
        etiqueta="Vista en directo"
        columnas={2}
        opciones={calidadesDeVista}
        valor={vista}
        alCambiar={(id) => setVista(id as CalidadVistaEnDirecto)}
      />
      <div className="flex flex-col gap-1">
        <label htmlFor="grabacion-de-video" className="text-sm font-medium text-gray-700">
          Vídeo de cada ejecución
        </label>
        <select
          id="grabacion-de-video"
          className="field sm:max-w-xs"
          value={grabacion}
          onChange={(e) => setGrabacion(e.target.value as GrabacionDeVideo)}
        >
          {opcionesDeGrabacion.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
        <p className="text-xs text-gray-500">Grabar lee la pantalla durante todo el caso, aunque nadie mire.</p>
      </div>
    </Apartado>
  )
}

function Clave({ despliegue, regenerando, alRegenerar }: { despliegue: DespliegueDto; regenerando: boolean; alRegenerar: () => void }) {
  const [confirmando, setConfirmando] = useState(false)
  return (
    <div className="flex flex-col gap-4">
      <p className="text-sm text-gray-600">
        Con esta clave se identifica el robot. Solo hace falta para un robot que corre en otra máquina: si lo arranca el lanzador de
        Viriato (subiendo su versión), no se usa.
      </p>
      <div className="grid gap-4 rounded-xl border border-gray-200 px-4 py-3 sm:grid-cols-2">
        <DatoRotulado rotulo="Clave">
          <span className="font-mono text-sm text-gray-700">{despliegue.apiKeyPrefix}…</span>
        </DatoRotulado>
        <DatoRotulado rotulo="Último uso">
          <span className="text-sm text-gray-700">{despliegue.lastUsedAt ? new Date(despliegue.lastUsedAt).toLocaleString() : 'Nunca'}</span>
        </DatoRotulado>
      </div>
      {confirmando ? (
        <div className="flex flex-col gap-3 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3">
          <p className="text-sm text-amber-900">
            La clave actual dejará de funcionar en cuanto generes otra: el robot que la use no podrá identificarse hasta que le pongas
            la nueva.
          </p>
          <div className="flex justify-end gap-2">
            <Button type="button" variant="ghost" size="sm" onClick={() => setConfirmando(false)}>
              Cancelar
            </Button>
            <Button
              type="button"
              size="sm"
              disabled={regenerando}
              onClick={() => {
                alRegenerar()
                setConfirmando(false)
              }}
            >
              {regenerando ? 'Generando…' : 'Generar otra'}
            </Button>
          </div>
        </div>
      ) : (
        <Button type="button" variant="secondary" className="w-fit" onClick={() => setConfirmando(true)}>
          Regenerar clave
        </Button>
      )}
    </div>
  )
}
