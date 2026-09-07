import { Activity, GitBranch, HardDrive, KeyRound, LayoutDashboard, LineChart, List, ListChecks, Settings, ShieldCheck, Users } from 'lucide-react'
import type { ReactNode } from 'react'

export interface NavLinkNode {
  type: 'link'
  to: string
  label: string
  icon: ReactNode
  /** Hidden unless the current user has this permission. Absent = visible to everyone. */
  permission?: string
}

export interface NavGroupNode {
  type: 'group'
  /** Stable key for remembering this group's expanded/collapsed state. */
  id: string
  label: string
  icon?: ReactNode
  children: NavNode[]
}

export type NavNode = NavLinkNode | NavGroupNode

// Three deliberately separate concepts, never merged into one "Administración" bucket:
//   - Monitorización: watching what's already happening (Casos activity) — Panel + Listado.
//   - Configuración general de procesos: defining/configuring the Procesos (Flujos) themselves.
//   - Configuración general: transversal app config — identity & access (Usuarios/Roles/Permisos).
// There is deliberately no "Casos" entry: a Caso only exists in the context of its Proceso, so
// creating one is an action that lives on that Proceso's own card (see ProcesoResumenCard), not a
// standalone menu item. Mercados/Trabajos are unrelated feature areas, left as flat top-level links.
export const navTree: NavNode[] = [
  {
    type: 'group',
    id: 'monitorizacion',
    label: 'Monitorización',
    icon: <Activity size={18} />,
    children: [
      { type: 'link', to: '/casos', label: 'Panel', icon: <LayoutDashboard size={18} /> },
      { type: 'link', to: '/casos/lista', label: 'Listado', icon: <List size={18} /> },
    ],
  },
  { type: 'link', to: '/mercados', label: 'Mercados', icon: <LineChart size={18} /> },
  { type: 'link', to: '/ops/trabajos', label: 'Trabajos', icon: <ListChecks size={18} /> },
  {
    type: 'group',
    id: 'config-procesos',
    label: 'Configuración general de procesos',
    icon: <GitBranch size={18} />,
    children: [
      { type: 'link', to: '/admin/flujos', label: 'Procesos', icon: <GitBranch size={18} />, permission: 'flujos.manage' },
      { type: 'link', to: '/admin/almacenamiento', label: 'Almacenamiento', icon: <HardDrive size={18} />, permission: 'flujos.manage' },
    ],
  },
  {
    type: 'group',
    id: 'config-general',
    label: 'Configuración general',
    icon: <Settings size={18} />,
    children: [
      { type: 'link', to: '/admin/usuarios', label: 'Usuarios', icon: <Users size={18} />, permission: 'users.manage' },
      { type: 'link', to: '/admin/roles', label: 'Roles', icon: <ShieldCheck size={18} />, permission: 'roles.manage' },
      { type: 'link', to: '/admin/permisos', label: 'Permisos', icon: <KeyRound size={18} />, permission: 'roles.manage' },
    ],
  },
]
