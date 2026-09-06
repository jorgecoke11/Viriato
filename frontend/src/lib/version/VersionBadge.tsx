import { useQuery } from '@tanstack/react-query'
import { getVersion } from './api'

// Doesn't change during a session and is harmless to cache indefinitely — no point refetching it.
export function VersionBadge() {
  const { data } = useQuery({ queryKey: ['app-version'], queryFn: getVersion, staleTime: Infinity })
  if (!data) return null

  return (
    <p className="mt-2 px-3 text-xs text-gray-400" title={data.commit ? `Commit ${data.commit}` : undefined}>
      v{data.version}
      {data.commit && <span className="font-mono"> · {data.commit}</span>}
    </p>
  )
}
