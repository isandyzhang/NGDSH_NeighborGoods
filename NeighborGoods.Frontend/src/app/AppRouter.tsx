import { Navigate, Route, Routes } from 'react-router-dom'
import { RequireAuth } from '@/features/auth/components/RequireAuth'
import { LoginPage } from '@/features/auth/pages/LoginPage'
import { ListingDetailPage } from '@/features/listings/pages/ListingDetailPage'
import { ListingHomePage } from '@/features/listings/pages/ListingHomePage'
import { ChatPage } from '@/features/messaging/pages/ChatPage'
import { ConversationsPage } from '@/features/messaging/pages/ConversationsPage'
import { AppLayout } from '@/app/AppLayout'

export const AppRouter = () => {
  return (
    <Routes>
      <Route element={<AppLayout />}>
        <Route path="/" element={<Navigate to="/listings" replace />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/listings" element={<ListingHomePage />} />
        <Route path="/listings/:id" element={<ListingDetailPage />} />
        <Route element={<RequireAuth />}>
          <Route path="/messages" element={<ConversationsPage />} />
          <Route path="/messages/:conversationId" element={<ChatPage />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/listings" replace />} />
    </Routes>
  )
}
