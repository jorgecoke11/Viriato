import type { ReactNode } from 'react'

/**
 * A value with a small caption above it ("SITUACIÓN" over a badge). For when several chips sit side by side and each one answers
 * a different question: without the caption they read as one list of tags. `ayuda` is the tooltip that says what it means.
 */
export function DatoRotulado({ rotulo, ayuda, children }: { rotulo: string; ayuda?: string; children: ReactNode }) {
  return (
    <div className="flex min-w-0 flex-col gap-1" title={ayuda}>
      <span className="text-[11px] font-medium tracking-wide text-gray-500 uppercase">{rotulo}</span>
      <span className="flex min-w-0 flex-wrap items-center gap-1.5">{children}</span>
    </div>
  )
}
