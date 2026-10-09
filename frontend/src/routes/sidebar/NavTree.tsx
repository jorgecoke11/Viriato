import { AnimatePresence, motion } from 'framer-motion'
import { ChevronDown } from 'lucide-react'
import { useState } from 'react'
import { useAuth } from '../../features/auth/useAuth'
import { spring } from '../../lib/motion/tokens'
import { collapseVariants } from '../../lib/motion/variants'
import { EnlaceDeMenu, EnlaceDeMenuCompacto } from './EnlaceDeMenu'
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
      <nav className="flex flex-1 flex-col items-center gap-1.5 overflow-y-auto px-2">
        {flattenLinks(visible).map((link) => (
          <EnlaceDeMenuCompacto key={link.to} to={link.to} end={link.end} icono={link.icon} etiqueta={link.label} onNavigate={onNavigate} />
        ))}
      </nav>
    )
  }

  const searching = normalizedQuery !== ''

  return (
    <nav aria-label="Principal" className="flex flex-1 flex-col gap-1 overflow-y-auto overflow-x-hidden px-3 pb-3">
      {visible.map((node) => {
        if (node.type === 'link') {
          return <EnlaceDeMenu key={node.to} to={node.to} end={node.end} icono={node.icon} etiqueta={node.label} onNavigate={onNavigate} />
        }

        const open = searching || (openGroups[node.id] ?? true)

        return (
          <div key={node.id} className="mt-3 border-t border-side-border pt-3 first:mt-0 first:border-t-0 first:pt-0">
            <button
              type="button"
              title={node.label}
              className="group flex w-full items-center gap-2.5 rounded-lg px-2 py-1.5 text-left text-side-strong"
              aria-expanded={open}
              onClick={() => setOpenGroups((prev) => ({ ...prev, [node.id]: !open }))}
            >
              <span aria-hidden="true" className="flex h-6 w-6 shrink-0 items-center justify-center rounded-md bg-indigo-500/20 text-side-accent [&>svg]:h-3.5 [&>svg]:w-3.5">
                {node.icon}
              </span>
              <span className="min-w-0 flex-1 truncate text-[12px] font-semibold tracking-[0.06em] uppercase opacity-90">{node.label}</span>
              <motion.span animate={{ rotate: open ? 180 : 0 }} transition={spring.snappy} className="shrink-0 text-side-text group-hover:text-side-strong">
                <ChevronDown size={14} />
              </motion.span>
            </button>

            <AnimatePresence initial={false}>
              {open && (
                <motion.div variants={collapseVariants} initial="initial" animate="animate" exit="exit" className="overflow-hidden">
                  <div className="flex flex-col gap-1 pt-1 pb-1">
                    {node.children.map((child) =>
                      child.type === 'link' ? (
                        <EnlaceDeMenu key={child.to} to={child.to} end={child.end} icono={child.icon} etiqueta={child.label} onNavigate={onNavigate} />
                      ) : null,
                    )}
                  </div>
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        )
      })}

      {visible.length === 0 && <p className="px-3 py-2 text-sm text-side-text">Sin resultados.</p>}
    </nav>
  )
}
