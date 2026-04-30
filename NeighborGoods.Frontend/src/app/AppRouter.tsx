import { Suspense, lazy } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { RequireAuth } from '@/features/auth/components/RequireAuth'
import { ListingHomePage } from '@/features/listings/pages/ListingHomePage'
import { SellerPage } from '@/features/seller/pages/SellerPage'
import { AppLayout } from '@/app/AppLayout'

const LineLoginCallbackPage = lazy(() =>
  import('@/features/auth/pages/LineLoginCallbackPage').then((module) => ({ default: module.LineLoginCallbackPage })),
)
const LoginPage = lazy(() => import('@/features/auth/pages/LoginPage').then((module) => ({ default: module.LoginPage })))
const RegisterPage = lazy(() =>
  import('@/features/auth/pages/RegisterPage').then((module) => ({ default: module.RegisterPage })),
)
const AccountPage = lazy(() => import('@/features/account/pages/AccountPage').then((module) => ({ default: module.AccountPage })))
const ContactAdminPage = lazy(() =>
  import('@/features/contact/pages/ContactAdminPage').then((module) => ({ default: module.ContactAdminPage })),
)
const FavoritesPage = lazy(() => import('@/features/favorites/pages/FavoritesPage').then((module) => ({ default: module.FavoritesPage })))
const PrivacyPage = lazy(() => import('@/features/legal/pages/PrivacyPage').then((module) => ({ default: module.PrivacyPage })))
const TermsPage = lazy(() => import('@/features/legal/pages/TermsPage').then((module) => ({ default: module.TermsPage })))
const CreateListingPage = lazy(() =>
  import('@/features/listings/pages/CreateListingPage').then((module) => ({ default: module.CreateListingPage })),
)
const EditListingPage = lazy(() =>
  import('@/features/listings/pages/EditListingPage').then((module) => ({ default: module.EditListingPage })),
)
const ListingDetailPage = lazy(() =>
  import('@/features/listings/pages/ListingDetailPage').then((module) => ({ default: module.ListingDetailPage })),
)
const MyListingsPage = lazy(() =>
  import('@/features/listings/pages/MyListingsPage').then((module) => ({ default: module.MyListingsPage })),
)
const ChatPage = lazy(() => import('@/features/messaging/pages/ChatPage').then((module) => ({ default: module.ChatPage })))
const ConversationsPage = lazy(() =>
  import('@/features/messaging/pages/ConversationsPage').then((module) => ({ default: module.ConversationsPage })),
)
const NotificationCenterPage = lazy(() =>
  import('@/features/notifications/pages/NotificationCenterPage').then((module) => ({
    default: module.NotificationCenterPage,
  })),
)
const CreateReviewPage = lazy(() =>
  import('@/features/reviews/pages/CreateReviewPage').then((module) => ({ default: module.CreateReviewPage })),
)
const ErrorPage = lazy(() => import('@/features/system/pages/ErrorPage').then((module) => ({ default: module.ErrorPage })))
const NotFoundPage = lazy(() =>
  import('@/features/system/pages/NotFoundPage').then((module) => ({ default: module.NotFoundPage })),
)

const RouteFallback = () => <div className="px-4 py-8 text-sm text-text-subtle">頁面載入中...</div>

export const AppRouter = () => {
  return (
    <Suspense fallback={<RouteFallback />}>
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
          <Route path="/listing" element={<Navigate to="/listings" replace />} />
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
    </Suspense>
  )
}
