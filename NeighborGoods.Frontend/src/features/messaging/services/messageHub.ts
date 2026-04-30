import {
  HubConnection,
  HubConnectionBuilder,
  LogLevel,
} from '@microsoft/signalr'
import { env } from '@/shared/config/env'
import type { MessageItem } from '@/features/messaging/api/messagingApi'

type MessageHandler = (message: MessageItem) => void

export class MessageHubClient {
  private connection: HubConnection | null = null
  private startPromise: Promise<void> | null = null
  private stopAfterStart = false
  private conversationId: string | null = null
  private disconnectTimer: number | null = null

  private clearPendingDisconnect() {
    if (this.disconnectTimer !== null) {
      window.clearTimeout(this.disconnectTimer)
      this.disconnectTimer = null
    }
  }

  async connect(getAccessToken: () => string | null, conversationId: string, onMessage: MessageHandler): Promise<void> {
    this.clearPendingDisconnect()
    // 新連線請求代表仍需保持連線，取消先前 cleanup 設下的「連上後立刻停止」旗標。
    this.stopAfterStart = false
    this.conversationId = conversationId
    if (this.startPromise) {
      await this.startPromise
      if (!this.connection) {
        await this.connect(getAccessToken, conversationId, onMessage)
        return
      }
      await this.joinConversationIfNeeded()
      return
    }

    if (this.connection) {
      await this.joinConversationIfNeeded()
      return
    }

    const connection = new HubConnectionBuilder()
      .withUrl(`${env.signalrBaseUrl}/hubs/messages`, {
        accessTokenFactory: () => getAccessToken() ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()
    this.connection = connection

    connection.on('ReceiveMessage', (message: MessageItem) => {
      onMessage(message)
    })
    connection.onreconnected(() => {
      void this.joinConversationIfNeeded()
    })

    this.startPromise = connection
      .start()
      .then(async () => {
        await this.joinConversationIfNeeded()
        if (!this.stopAfterStart) {
          return
        }

        this.stopAfterStart = false
        await connection.stop()
        this.connection = null
      })
      .catch((error) => {
        console.warn('[SignalR] connect failed', error)
        this.connection = null
        throw error
      })
      .finally(() => {
        this.startPromise = null
      })

    await this.startPromise
  }

  private async joinConversationIfNeeded(): Promise<void> {
    if (!this.connection || this.connection.state !== 'Connected' || !this.conversationId) {
      return
    }

    await this.connection.invoke('JoinConversation', this.conversationId)
  }

  async disconnect(): Promise<void> {
    if (!this.connection) {
      return
    }

    if (this.startPromise) {
      // 若正在 negotiation，先標記，等 start 完再停，避免 AbortError 噪音。
      this.stopAfterStart = true
      try {
        await this.startPromise
      } catch {
        // 啟動已失敗時不需額外處理
      }
      return
    }

    const connection = this.connection
    this.clearPendingDisconnect()
    this.disconnectTimer = window.setTimeout(() => {
      void connection.stop().finally(() => {
        if (this.connection === connection) {
          this.connection = null
        }
        this.disconnectTimer = null
      })
    }, 1500)
  }
}
