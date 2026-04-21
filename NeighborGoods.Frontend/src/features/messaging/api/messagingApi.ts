import { http } from '@/shared/api/http'
import { unwrapApiResponse, type ApiResponse } from '@/shared/types/api'

export type ConversationItem = {
  conversationId: string
  listingId: string
  listingTitle: string
  otherUserId: string
  otherDisplayName: string
  lastMessagePreview: string | null
  lastMessageAt: string | null
  unreadCount: number
}

export type MessageItem = {
  id: string
  senderId: string
  senderDisplayName: string
  content: string
  createdAt: string
}

export type MessagesPage = {
  items: MessageItem[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

type ConversationsPayload = { items: ConversationItem[] }

export const messagingApi = {
  async ensureConversation(listingId: string, otherUserId: string): Promise<string> {
    const response = await http.post<ApiResponse<{ conversationId: string }>>('/api/v1/conversations', {
      listingId,
      otherUserId,
    })
    return unwrapApiResponse(response.data).conversationId
  },

  async listConversations(): Promise<ConversationItem[]> {
    const response = await http.get<ApiResponse<ConversationsPayload>>('/api/v1/conversations')
    return unwrapApiResponse(response.data).items
  },

  async getMessages(conversationId: string, page = 1, pageSize = 50): Promise<MessagesPage> {
    const response = await http.get<ApiResponse<MessagesPage>>(
      `/api/v1/conversations/${conversationId}/messages`,
      {
        params: { page, pageSize },
      },
    )
    return unwrapApiResponse(response.data)
  },

  async sendMessage(conversationId: string, content: string): Promise<MessageItem> {
    const response = await http.post<ApiResponse<MessageItem>>(
      `/api/v1/conversations/${conversationId}/messages`,
      { content },
    )
    return unwrapApiResponse(response.data)
  },

  async markRead(conversationId: string): Promise<void> {
    await http.post(`/api/v1/conversations/${conversationId}/read`)
  },
}
