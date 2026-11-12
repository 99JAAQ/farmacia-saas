import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { getAccessToken, refreshSession } from '@/lib/api/http'
import { invalidationsFor, type RealtimeChange } from './invalidations'

const hubPath = '/hubs/pharmacy'

/**
 * Keeps the screens of this pharmacy current: when the API says something changed, the queries showing it fetch
 * again. Real time is a convenience, the API is the truth: after any gap in the connection (reconnect, a failed
 * start) every query is refreshed, since notices may have been missed.
 *
 * @param enabled Only for a pharmacy's user who can work: the platform administrator and a user with a temporary
 * password are refused by the hub.
 */
export function useRealtime(enabled: boolean) {
  const queryClient = useQueryClient()

  useEffect(() => {
    if (!enabled) return

    let stopped = false
    let retry: ReturnType<typeof setTimeout> | undefined

    const connection = new HubConnectionBuilder()
      // The token is read at every (re)connection: the 15-minute token is renewed by the HTTP client.
      .withUrl(hubPath, { accessTokenFactory: () => getAccessToken() ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('changed', (changes: RealtimeChange[]) => {
      for (const queryKey of invalidationsFor(changes)) void queryClient.invalidateQueries({ queryKey })
    })
    connection.onreconnected(() => void queryClient.invalidateQueries())

    const start = async (attempt: number) => {
      try {
        await connection.start()
        if (attempt > 0) void queryClient.invalidateQueries()
      } catch {
        if (stopped) return
        // Most often the token expired while the connection was down (a power or internet cut): renew it and retry,
        // waiting longer each time, up to 30 seconds. A network error never ends the session (see http.ts).
        await refreshSession().catch(() => null)
        retry = setTimeout(() => void start(attempt + 1), Math.min(30_000, 2_000 * 2 ** attempt))
      }
    }

    // When the automatic reconnection gives up, start over with the same backoff.
    connection.onclose(() => {
      if (!stopped) void start(1)
    })
    void start(0)

    return () => {
      stopped = true
      clearTimeout(retry)
      void connection.stop()
    }
  }, [enabled, queryClient])
}
