import { Bot, Layers, ShieldCheck, Workflow } from 'lucide-react'
import type { ReactNode } from 'react'

const puntos = [
  { icon: Layers, titulo: 'Casos con seguimiento', texto: 'Cada caso con su historial, documentos y evidencias en un solo sitio.' },
  { icon: Bot, titulo: 'Robots coordinados', texto: 'Reparte el trabajo entre equipos y servicios y mira qué está ocurriendo.' },
  { icon: ShieldCheck, titulo: 'Credenciales protegidas', texto: 'Las contraseñas de los robots viven cifradas, nunca en su configuración.' },
]

/** The frame of the sign-in and registration screens: the product on one side, the form on the other. On a
 *  phone the product panel steps aside and the form takes the whole screen. */
export function AuthShell({ titulo, descripcion, children, pie }: { titulo: string; descripcion: string; children: ReactNode; pie: ReactNode }) {
  return (
    <div className="grid min-h-screen bg-page lg:grid-cols-[minmax(0,1fr)_minmax(0,1.1fr)]">
      <aside className="relative hidden overflow-hidden bg-side p-12 text-side-strong lg:flex lg:flex-col lg:justify-between">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute -top-32 -left-24 h-96 w-96 rounded-full bg-indigo-600/30 blur-3xl"
        />
        <div
          aria-hidden="true"
          className="pointer-events-none absolute -right-24 bottom-0 h-96 w-96 rounded-full bg-blue-600/20 blur-3xl"
        />
        <div className="relative flex items-center gap-3">
          <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-400 to-indigo-600 text-white shadow-[0_4px_14px_-4px_rgb(99_102_241/0.7)]">
            <Workflow size={20} />
          </span>
          <span className="text-xl font-semibold tracking-tight">Viariato</span>
        </div>

        <div className="relative flex max-w-md flex-col gap-8">
          <h2 className="text-3xl leading-tight font-semibold tracking-tight">Tus procesos y tus robots, bajo control.</h2>
          <ul className="flex flex-col gap-5">
            {puntos.map(({ icon: Icon, titulo: t, texto }) => (
              <li key={t} className="flex gap-4">
                <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-white/10 text-indigo-300">
                  <Icon size={18} />
                </span>
                <div>
                  <p className="text-sm font-medium">{t}</p>
                  <p className="text-sm text-side-text">{texto}</p>
                </div>
              </li>
            ))}
          </ul>
        </div>

        <p className="relative text-xs text-side-text">Procesos y robots</p>
      </aside>

      <main className="flex items-center justify-center px-4 py-10 sm:px-8">
        <div className="w-full max-w-sm">
          <div className="mb-8 flex items-center gap-3 lg:hidden">
            <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-400 to-indigo-600 text-white">
              <Workflow size={18} />
            </span>
            <span className="text-lg font-semibold tracking-tight text-gray-900">Viariato</span>
          </div>
          <h1 className="page-title">{titulo}</h1>
          <p className="mt-1 mb-6 text-sm text-gray-500">{descripcion}</p>
          {children}
          <p className="mt-6 text-sm text-gray-500">{pie}</p>
        </div>
      </main>
    </div>
  )
}
