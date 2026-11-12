import { useQuery } from '@tanstack/react-query'
import { useAuth } from '@/features/auth/useAuth'
import { getPharmacy, pharmacyKeys } from './api'

/**
 * The current pharmacy, loaded once per session (the layout asks for it, so it is already cached when a ticket is
 * printed). The platform administrator has no pharmacy, so it does not ask.
 */
export function usePharmacy() {
  const { user } = useAuth()
  return useQuery({
    queryKey: pharmacyKeys.current,
    queryFn: ({ signal }) => getPharmacy(signal),
    enabled: user?.pharmacyId != null,
    staleTime: 10 * 60 * 1000,
  })
}
