/** Viriato's mark: the head with the red band, on its gradient. Sized by the caller (h-/w- classes). */
export function Logo({ className = 'h-10 w-10' }: { className?: string }) {
  return <img src="/logo.png" alt="" aria-hidden="true" draggable={false} className={`shrink-0 rounded-lg object-cover ${className}`} />
}
