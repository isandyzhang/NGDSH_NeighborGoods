import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import { useAuth } from '@/features/auth/components/AuthProvider'
import { messagingApi } from '@/features/messaging/api/messagingApi'
import { env } from '@/shared/config/env'

const UNREAD_FALLBACK_POLL_MS = 5 * 60 * 1000

type UnreadTotalUpdatedPayload = { totalUnread: number }

export type SharedMessageHubContextValue = {
  connection: HubConnection | null
  hubReady: boolean
  totalUnread: number
  refreshUnreadSummary: () => Promise<void>
  joinConversation: (conversationId: string) => Promise<void>
  leaveConversation: (conversationId: string) => Promise<void>
}

const SharedMessageHubContext = createContext<SharedMessageHubContextValue | null>(null)

export function SharedMessageHubProvider({ children }: { children: ReactNode }) {
  const { isAuthenticated, tokens } = useAuth()
  const [totalUnread, setTotalUnread] = useState(0)
  const [hubReady, setHubReady] = useState(false)
  const [connection, setConnection] = useState<HubConnection | null>(null)
  const accessTokenRef = useRef<string | null>(null)

  useEffect(() => {
    accessTokenRef.current = tokens?.accessToken ?? null
  }, [tokens?.accessToken])

  const refreshUnreadSummary = useCallback(async () => {
    try {
      const n = await messagingApi.getUnreadSummary()
      setTotalUnread(Math.max(0, n))
    } catch {
      /* 保留上一次未讀數 */
    }
  }, [])

  useEffect(() => {
    if (!isAuthenticated) {
      setTotalUnread(0)
      setHubReady(false)
      setConnection((current) => {
        if (current) {
          void current.stop()
        }
        return null
      })
      return
    }

    let disposed = false

    const conn = new HubConnectionBuilder()
      .withUrl(`${env.signalrBaseUrl}/hubs/messages`, {
        accessTokenFactory: () => accessTokenRef.current ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    setConnection(conn)

    conn.on('UnreadTotalUpdated', (payload: UnreadTotalUpdatedPayload) => {
      if (payload && typeof payload.totalUnread === 'number' && !Number.isNaN(payload.totalUnread)) {
        setTotalUnread(Math.max(0, payload.totalUnread))
      }
    })

    void conn
      .start()
      .then(() => {
        if (disposed) {
          return
        }
        setHubReady(true)
        return refreshUnreadSummary()
      })
      .catch((err: unknown) => {
        if (!disposed) {
          console.warn('[SignalR] app hub connect failed', err)
        }
      })

    conn.onreconnected(() => {
      void refreshUnreadSummary()
    })

    return () => {
      disposed = true
      setHubReady(false)
      void conn.stop()
      setConnection((current) => (current === conn ? null : current))
    }
  }, [isAuthenticated, tokens?.userId, refreshUnreadSummary])

  useEffect(() => {
    if (!isAuthenticated || !hubReady) {
      return
    }

    const pollId = window.setInterval(() => {
      if (document.visibilityState !== 'visible') {
        return
      }
      void refreshUnreadSummary()
    }, UNREAD_FALLBACK_POLL_MS)

    const onVisibilityChange = () => {
      if (document.visibilityState === 'visible') {
        void refreshUnreadSummary()
      }
    }

    document.addEventListener('visibilitychange', onVisibilityChange)

    return () => {
      window.clearInterval(pollId)
      document.removeEventListener('visibilitychange', onVisibilityChange)
    }
  }, [isAuthenticated, hubReady, refreshUnreadSummary])

  const joinConversation = useCallback(async (conversationId: string) => {
    if (!connection || connection.state !== 'Connected') {
      return
    }
    await connection.invoke('JoinConversation', conversationId)
  }, [connection])

  const leaveConversation = useCallback(async (conversationId: string) => {
    if (!connection || connection.state !== 'Connected') {
      return
    }
    try {
      await connection.invoke('LeaveConversation', conversationId)
    } catch {
      /* 離開群組失敗時略過 */
    }
  }, [connection])

  const value = useMemo<SharedMessageHubContextValue>(
    () => ({
      connection,
      hubReady,
      totalUnread,
      refreshUnreadSummary,
      joinConversation,
      leaveConversation,
    }),
    [connection, hubReady, totalUnread, refreshUnreadSummary, joinConversation, leaveConversation],
  )

  return <SharedMessageHubContext.Provider value={value}>{children}</SharedMessageHubContext.Provider>
}

export function useSharedMessageHub(): SharedMessageHubContextValue {
  const ctx = useContext(SharedMessageHubContext)
  if (!ctx) {
    throw new Error('useSharedMessageHub 必須在 SharedMessageHubProvider 內使用')
  }
  return ctx
}
