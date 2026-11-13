import { useQuery } from '@tanstack/react-query'
import * as rpaApi from './api'

/** The versions of a service's robot, shared by all its despliegues. */
export const claveDeVersiones = (servicioId: string) => ['versiones-de-robot', servicioId] as const

export function useVersionesDelServicio(servicioId: string, habilitada = true) {
  return useQuery({ queryKey: claveDeVersiones(servicioId), queryFn: () => rpaApi.listVersionesDeRobot(servicioId), enabled: habilitada })
}
