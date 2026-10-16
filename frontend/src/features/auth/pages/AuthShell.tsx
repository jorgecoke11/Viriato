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
      <aside className="hidden border-r border-gray-200 bg-gray-100 p-12 text-gray-900 lg:flex lg:flex-col lg:justify-between">
        <div className="flex items-center gap-3">
          <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-indigo-600 text-white">
            <Workflow size={20} />
          </span>
          <span className="text-xl font-semibold tracking-tight">Viariato</span>
        </div>

        <div className="flex max-w-md flex-col gap-8">
          <h2 className="text-3xl leading-tight font-semibold tracking-tight">Tus procesos y tus robots, bajo control.</h2>
          <ul className="flex flex-col gap-5">
            {puntos.map(({ icon: Icon, titulo: t, texto }) => (
              <li key={t} className="flex gap-4">
                <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-indigo-100 text-indigo-700">
                  <Icon size={18} />
                </span>
                <div>
                  <p className="text-sm font-medium">{t}</p>
                  <p className="text-sm text-gray-500">{texto}</p>
                </div>
              </li>
            ))}
          </ul>
        </div>

        <p className="text-xs text-gray-500">Procesos y robots</p>
      </aside>

      <main className="flex items-center justify-center px-4 py-10 sm:px-8">
        <div className="w-full max-w-sm">
          <div className="mb-8 flex items-center gap-3 lg:hidden">
            <span className="flex h-9 w-9 items-center justify-center rounded-lg bg-indigo-600 text-white">
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
