import { FilePlus2 } from 'lucide-react'
import { Badge } from '../../components/ui/Badge'
import type { OpcionEnTarjeta } from '../../components/ui/TarjetasDeOpciones'
import type { CreadorDisponibleDto } from './api'

/** The creators as cards: what it is called, what it is for and which process (and type of case) it belongs to. */
export function opcionesDeCreadores(creadores: readonly CreadorDisponibleDto[]): OpcionEnTarjeta[] {
  return creadores.map((c) => ({
    id: c.id,
    titulo: c.nombre,
    descripcion: c.descripcion,
    icono: FilePlus2,
    detalle: (
      <>
        <span>{c.flujoNombre}</span>
        {c.tipoCasoNombre && (
          <Badge tone="brand" dot={false}>
            {c.tipoCasoNombre}
          </Badge>
        )}
      </>
    ),
  }))
}
