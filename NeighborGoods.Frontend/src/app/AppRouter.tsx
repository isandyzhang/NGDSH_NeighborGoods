import { Navigate, Route, Routes } from 'react-router-dom'
import { RequireAuth } from '@/features/auth/components/RequireAuth'
import { LineLoginCallbackPage } from '@/features/auth/pages/LineLoginCallbackPage'
import { LoginPage } from '@/features/auth/pages/LoginPage'
import { RegisterPage } from '@/features/auth/pages/RegisterPage'
import { AccountPage } from '@/features/account/pages/AccountPage'
import { ContactAdminPage } from '@/features/contact/pages/ContactAdminPage'
import { FavoritesPage } from '@/features/favorites/pages/FavoritesPage'
import { PrivacyPage } from '@/features/legal/pages/PrivacyPage'
import { TermsPage } from '@/features/legal/pages/TermsPage'
import { CreateListingPage } from '@/features/listings/pages/CreateListingPage'
import { EditListingPage } from '@/features/listings/pages/EditListingPage'
import { ListingDetailPage } from '@/features/listings/pages/ListingDetailPage'
import { ListingHomePage } from '@/features/listings/pages/ListingHomePage'
import { MyListingsPage } from '@/features/listings/pages/MyListingsPage'
import { ChatPage } from '@/features/messaging/pages/ChatPage'
import { ConversationsPage } from '@/features/messaging/pages/ConversationsPage'
import { NotificationCenterPage } from '@/features/notifications/pages/NotificationCenterPage'
import { CreateReviewPage } from '@/features/reviews/pages/CreateReviewPage'
import { SellerPage } from '@/features/seller/pages/SellerPage'
import { ErrorPage } from '@/features/system/pages/ErrorPage'
import { NotFoundPage } from '@/features/system/pages/NotFoundPage'
import { AppLayout } from '@/app/AppLayout'

export const AppRouter = () => {
  return (
    <Routes>
      <Route element={<AppLayout />}>
        <Route path="/" element={<Navigate to="/listings" replace />} />
        <Route path="/login" element={<LoginPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/auth/line/callback" element={<LineLoginCallbackPage />} />
        <Route path="/terms" element={<TermsPage />} />
        <Route path="/privacy" element={<PrivacyPage />} />
        <Route path="/error" element={<ErrorPage />} />
        <Route path="/contact-admin" element={<ContactAdminPage />} />
        <Route path="/listings" element={<ListingHomePage />} />
        <Route path="/listings/:id" element={<ListingDetailPage />} />
        <Route path="/seller/:sellerId" element={<SellerPage />} />
        <Route element={<RequireAuth />}>
          <Route path="/account" element={<AccountPage />} />
          <Route path="/notifications" element={<NotificationCenterPage />} />
          <Route path="/favorites" element={<FavoritesPage />} />
          <Route path="/my-listings" element={<MyListingsPage />} />
          <Route path="/listings/create" element={<CreateListingPage />} />
          <Route path="/listings/:id/edit" element={<EditListingPage />} />
          <Route path="/messages" element={<ConversationsPage />} />
          <Route path="/messages/:conversationId" element={<ChatPage />} />
          <Route path="/purchase-requests/:requestId/review" element={<CreateReviewPage />} />
        </Route>
      </Route>
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}
