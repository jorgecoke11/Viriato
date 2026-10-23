import { VisorDeDatos } from '../../components/ui/VisorDeDatos'

/** The business data of the Caso — what the robots of the process work from — as a readable list (see `VisorDeDatos`). */
export function DatosDelCasoCard({ datosJson }: { datosJson: string | null }) {
  return <VisorDeDatos titulo="Datos del caso" json={datosJson} textoVacio="Este caso no tiene datos de negocio." />
}
