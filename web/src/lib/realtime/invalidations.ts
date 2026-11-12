import type { QueryKey } from '@tanstack/react-query'

/** A notice from the API: what changed, never the data (see ADR 0017). */
export interface RealtimeChange {
  topic: string
  id: string | null
}

/**
 * The queries each topic makes stale, by prefix (TanStack Query invalidates every key that starts with it). Only the
 * queries on screen fetch again; the rest are fetched fresh when the user goes back to them.
 */
const keysByTopic: Record<string, QueryKey[]> = {
  // A sale or a return changes the shift's sales and its expected cash.
  sales: [['sales'], ['cash-sessions']],
  // Stock lives under products: the catalog, a product's batches and kardex, and the POS search.
  stock: [['products']],
  catalog: [['products'], ['categories']],
  purchases: [['purchases']],
  cash: [['cash-sessions'], ['sales']],
  // A customer's account and statement, and who owes.
  credit: [['customers']],
  customers: [['customers']],
  suppliers: [['suppliers']],
  pharmacy: [['pharmacy']],
  users: [['users']],
}

/** The distinct query prefixes to invalidate for a batch of notices. Unknown topics are ignored (a newer API). */
export function invalidationsFor(changes: readonly RealtimeChange[]): QueryKey[] {
  const unique = new Map<string, QueryKey>()
  for (const change of changes)
    for (const key of keysByTopic[change.topic] ?? []) unique.set(JSON.stringify(key), key)
  return [...unique.values()]
}
