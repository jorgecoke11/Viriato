import { apiFetch } from '../apiClient'

export interface VersionInfo {
  version: string
  commit: string | null
}

export const getVersion = () => apiFetch<VersionInfo>('/version')
