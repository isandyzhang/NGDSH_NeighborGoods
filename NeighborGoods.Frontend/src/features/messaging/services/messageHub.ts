import {
  HubConnection,
  HubConnectionBuilder,
  HttpTransportType,
  LogLevel,
} from '@microsoft/signalr'
import { env } from '@/shared/config/env'
import type { MessageItem } from '@/features/messaging/api/messagingApi'

type MessageHandler = (message: MessageItem) => void

export class MessageHubClient {
  private connection: HubConnection | null = null

  async connect(accessToken: string, onMessage: MessageHandler): Promise<void> {
    if (this.connection) {
      return
    }

    this.connection = new HubConnectionBuilder()
      .withUrl(`${env.signalrBaseUrl}/hubs/messages`, {
        accessTokenFactory: () => accessToken,
        transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    this.connection.on('ReceiveMessage', (message: MessageItem) => {
      onMessage(message)
    })

    await this.connection.start()
  }

  async disconnect(): Promise<void> {
    if (!this.connection) {
      return
    }

    await this.connection.stop()
    this.connection = null
  }
}
