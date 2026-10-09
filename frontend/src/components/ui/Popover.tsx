import { AnimatePresence, motion } from 'framer-motion'
import { useCallback, useEffect, useId, useLayoutEffect, useRef, useState, type ReactNode } from 'react'
import { createPortal } from 'react-dom'

const GAP = 6
const MARGEN = 8

interface PopoverProps {
  /** The element that opens it. It receives what it needs to be a proper disclosure button. */
  trigger: (props: { open: boolean; onClick: () => void; ref: (el: HTMLElement | null) => void; 'aria-haspopup': 'dialog'; 'aria-expanded': boolean; 'aria-controls': string }) => ReactNode
  /** The content. `close` closes the popover and gives the focus back to the trigger. */
  children: (close: () => void) => ReactNode
  /** Names the panel for people using a screen reader. */
  label: string
  width?: number
}

/**
 * A small panel anchored to the element that opened it — for a choice too small to deserve a modal. It is drawn on top
 * of everything (so a scrolling list or a dialog never clips it), stays inside the window, closes on Escape, on a click
 * outside or when what it hangs from scrolls away, and puts the focus back on the trigger when it closes. Escape only
 * closes the popover: a dialog underneath it stays open.
 */
export function Popover({ trigger, children, label, width = 288 }: PopoverProps) {
  const [open, setOpen] = useState(false)
  const [posicion, setPosicion] = useState<{ top: number; left: number } | null>(null)
  const anclaRef = useRef<HTMLElement | null>(null)
  const panelRef = useRef<HTMLDivElement | null>(null)
  const id = useId()

  const close = useCallback(() => {
    setOpen(false)
    anclaRef.current?.focus()
  }, [])

  // Below the trigger and aligned to its right edge; above it if there is no room below; always inside the window.
  useLayoutEffect(() => {
    if (!open || !anclaRef.current) return
    const ancla = anclaRef.current.getBoundingClientRect()
    const alto = panelRef.current?.offsetHeight ?? 0
    const left = Math.min(Math.max(MARGEN, ancla.right - width), window.innerWidth - width - MARGEN)
    const cabeAbajo = ancla.bottom + GAP + alto <= window.innerHeight - MARGEN
    const top = cabeAbajo ? ancla.bottom + GAP : Math.max(MARGEN, ancla.top - GAP - alto)
    setPosicion({ top, left })
  }, [open, width, children])

  useEffect(() => {
    if (!open) return

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape') return
      event.stopImmediatePropagation()
      close()
    }
    const onPointerDown = (event: PointerEvent) => {
      const dentro = (el: Element | null) => !!el && event.target instanceof Node && el.contains(event.target)
      if (!dentro(panelRef.current) && !dentro(anclaRef.current)) setOpen(false)
    }
    const onScroll = (event: Event) => {
      if (panelRef.current && event.target instanceof Node && panelRef.current.contains(event.target)) return
      setOpen(false)
    }

    // In the capture phase, ahead of the Escape listener of any dialog the trigger lives in.
    document.addEventListener('keydown', onKeyDown, true)
    document.addEventListener('pointerdown', onPointerDown, true)
    window.addEventListener('scroll', onScroll, true)
    window.addEventListener('resize', close)
    return () => {
      document.removeEventListener('keydown', onKeyDown, true)
      document.removeEventListener('pointerdown', onPointerDown, true)
      window.removeEventListener('scroll', onScroll, true)
      window.removeEventListener('resize', close)
    }
  }, [open, close])

  return (
    <>
      {trigger({
        open,
        onClick: () => {
          setPosicion(null)
          setOpen((o) => !o)
        },
        ref: (el) => {
          anclaRef.current = el
        },
        'aria-haspopup': 'dialog',
        'aria-expanded': open,
        'aria-controls': id,
      })}
      {createPortal(
        <AnimatePresence>
          {open && (
            <motion.div
              ref={panelRef}
              id={id}
              role="dialog"
              aria-label={label}
              className="fixed z-[70] rounded-xl border border-gray-200 bg-raised p-4 shadow-pop"
              style={{ width, top: posicion?.top ?? -9999, left: posicion?.left ?? -9999, visibility: posicion ? 'visible' : 'hidden' }}
              initial={{ opacity: 0, y: -4, scale: 0.98 }}
              animate={{ opacity: 1, y: 0, scale: 1 }}
              exit={{ opacity: 0, y: -4, scale: 0.98 }}
              transition={{ duration: 0.12 }}
              onClick={(e) => e.stopPropagation()}
            >
              {children(close)}
            </motion.div>
          )}
        </AnimatePresence>,
        document.body,
      )}
    </>
  )
}
