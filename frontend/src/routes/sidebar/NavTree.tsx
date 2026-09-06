import { AnimatePresence, motion } from 'framer-motion'
import { ChevronDown } from 'lucide-react'
import { useState } from 'react'
import { NavLink } from 'react-router-dom'
import { useAuth } from '../../features/auth/useAuth'
import { spring } from '../../lib/motion/tokens'
import { collapseVariants } from '../../lib/motion/variants'
import type { NavNode } from './navConfig'

// Built from character codes rather than a \u escape literal in a regex class, which is easy to
// mangle in transit — this range is the Unicode combining diacritical marks NFD decomposition
// leaves behind (e.g. "á" -> "a" + this mark), so stripping it makes search accent-insensitive.
const COMBINING_MARKS = new RegExp(`[${String.fromCharCode(0x0300)}-${String.fromCharCode(0x036f)}]`, 'g')

function normalize(value: string): string {
  return value
    .normalize('NFD')
    .replace(COMBINING_MARKS, '')
    .toLowerCase()
    .trim()
}

/** A group is visible if it has at least one permitted descendant — it carries no permission of its own. */
function isPermitted(node: NavNode, can: (permission: string) => boolean): boolean {
  if (node.type === 'link') return !node.permission || can(node.permission)
  return node.children.some((child) => isPermitted(child, can))
}

function matches(label: string, query: string): boolean {
  return query === '' || normalize(label).includes(query)
}

/** Prunes the tree down to what this user may see and, while searching, what actually matches. */
function filterTree(nodes: NavNode[], can: (permission: string) => boolean, query: string): NavNode[] {
  const result: NavNode[] = []

  for (const node of nodes) {
    if (!isPermitted(node, can)) continue

    if (node.type === 'link') {
      if (matches(node.label, query)) result.push(node)
      continue
    }

    const matchingChildren = filterTree(node.children, can, query)
    const groupLabelMatches = matches(node.label, query)

    if (query === '' || groupLabelMatches || matchingChildren.length > 0) {
      // The group's own label matching should surface every child underneath it, not just the
      // ones that separately happen to match the same text.
      const children = query !== '' && groupLabelMatches ? filterTree(node.children, can, '') : matchingChildren
      result.push({ ...node, children })
    }
  }

  return result
}

/** Icon-rail mode has no room for group headers — every reachable link is shown flat, its own icon its only identity. */
function flattenLinks(nodes: NavNode[]): Extract<NavNode, { type: 'link' }>[] {
  return nodes.flatMap((node) => (node.type === 'link' ? [node] : flattenLinks(node.children)))
}

export const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  `flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
    isActive
      ? 'bg-gradient-to-r from-blue-600 to-indigo-700 text-white shadow-sm'
      : 'text-gray-600 hover:bg-blue-50 hover:text-blue-700'
  }`

export function NavTree({
  nodes,
  collapsed,
  query,
  onNavigate,
}: {
  nodes: NavNode[]
  collapsed: boolean
  query: string
  onNavigate?: () => void
}) {
  const { can } = useAuth()
  const [openGroups, setOpenGroups] = useState<Record<string, boolean>>({})
  const normalizedQuery = normalize(query)
  const visible = filterTree(nodes, can, normalizedQuery)

  if (collapsed) {
    return (
      <nav className="flex flex-1 flex-col items-center gap-1 overflow-y-auto px-2">
        {flattenLinks(visible).map((link) => (
          <NavLink
            key={link.to}
            to={link.to}
            title={link.label}
            aria-label={link.label}
            onClick={onNavigate}
            className={({ isActive }) =>
              `flex h-10 w-10 items-center justify-center rounded-lg transition-colors ${
                isActive ? 'bg-gradient-to-br from-blue-600 to-indigo-700 text-white shadow-sm' : 'text-gray-500 hover:bg-blue-50 hover:text-blue-700'
              }`
            }
          >
            {link.icon}
          </NavLink>
        ))}
      </nav>
    )
  }

  const searching = normalizedQuery !== ''

  return (
    <nav className="flex flex-1 flex-col gap-1 overflow-y-auto overflow-x-hidden px-3">
      {visible.map((node) => {
        if (node.type === 'link') {
          return (
            <NavLink key={node.to} to={node.to} className={navLinkClass} onClick={onNavigate}>
              {node.icon}
              {node.label}
            </NavLink>
          )
        }

        const open = searching || (openGroups[node.id] ?? true)

        return (
          <div key={node.id} className="mt-1 first:mt-0">
            <button
              type="button"
              title={node.label}
              className="flex w-full items-center gap-2 rounded-lg border-l-4 border-l-blue-500 bg-blue-50/70 px-3 py-2 text-left"
              aria-expanded={open}
              onClick={() => setOpenGroups((prev) => ({ ...prev, [node.id]: !open }))}
            >
              {node.icon}
              <span className="min-w-0 flex-1 truncate text-sm font-bold text-blue-950">{node.label}</span>
              <motion.span animate={{ rotate: open ? 180 : 0 }} transition={spring.snappy} className="shrink-0 text-blue-400">
                <ChevronDown size={16} />
              </motion.span>
            </button>

            <AnimatePresence initial={false}>
              {open && (
                <motion.div variants={collapseVariants} initial="initial" animate="animate" exit="exit" className="overflow-hidden">
                  <div className="flex flex-col gap-1 py-1 pl-4">
                    {node.children.map((child) =>
                      child.type === 'link' ? (
                        <NavLink key={child.to} to={child.to} className={navLinkClass} onClick={onNavigate}>
                          {child.icon}
                          {child.label}
                        </NavLink>
                      ) : null,
                    )}
                  </div>
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        )
      })}

      {visible.length === 0 && <p className="px-3 py-2 text-sm text-gray-400">Sin resultados.</p>}
    </nav>
  )
}
