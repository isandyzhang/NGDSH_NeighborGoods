import { Suspense, lazy, useEffect, useState } from 'react'
import { Navigate, Route, Routes, useLocation } from 'react-router-dom'
import { ensureLiffReady } from '@/features/listings/utils/lineShare'
import { RequireAuth } from '@/features/auth/components/RequireAuth'
import { RequireAdmin } from '@/features/admin/components/RequireAdmin'
import { ListingHomePage } from '@/features/listings/pages/ListingHomePage'
import { SellerPage } from '@/features/seller/pages/SellerPage'
import { AppLayout } from '@/app/AppLayout'
import {
  adminLiffDebugUrlNeedsCleanup,
  buildCleanAdminLiffDebugSearch,
  isAdminLiffDebugEntry,
} from '@/features/admin/liffInitDebug'
import { isLineNotifyBindingEntry, isListingShareEntry, resolveLiffEntryTarget } from '@/app/liffRoute'
import {
  buildCleanListingShareEntrySearch,
  listingShareEntryNeedsCleanup,
} from '@/features/listings/listingShareSession'

const LineLoginCallbackPage = lazy(() =>
  import('@/features/auth/pages/LineLoginCallbackPage').then((module) => ({ default: module.LineLoginCallbackPage })),
)
const LoginPage = lazy(() => import('@/features/auth/pages/LoginPage').then((module) => ({ default: module.LoginPage })))
const RegisterPage = lazy(() =>
  import('@/features/auth/pages/RegisterPage').then((module) => ({ default: module.RegisterPage })),
)
const AccountPage = lazy(() => import('@/features/account/pages/AccountPage').then((module) => ({ default: module.AccountPage })))
const AccountEmailVerifyPage = lazy(() =>
  import('@/features/account/pages/AccountEmailVerifyPage').then((module) => ({ default: module.AccountEmailVerifyPage })),
)
const LineNotifyLiffPage = lazy(() =>
  import('@/features/account/pages/LineNotifyLiffPage').then((module) => ({ default: module.LineNotifyLiffPage })),
)
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
const ListingShareLiffPage = lazy(() =>
  import('@/features/listings/pages/ListingShareLiffPage').then((module) => ({ default: module.ListingShareLiffPage })),
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
const AdminLayout = lazy(() =>
  import('@/features/admin/layout/AdminLayout').then((module) => ({ default: module.AdminLayout })),
)
const AdminDashboardPage = lazy(() =>
  import('@/features/admin/pages/AdminDashboardPage').then((module) => ({ default: module.AdminDashboardPage })),
)
const AdminAnnouncementsPage = lazy(() =>
  import('@/features/admin/pages/AdminAnnouncementsPage').then((module) => ({ default: module.AdminAnnouncementsPage })),
)
const AdminListingsPage = lazy(() =>
  import('@/features/admin/pages/AdminListingsPage').then((module) => ({ default: module.AdminListingsPage })),
)
const AdminMembersPage = lazy(() =>
  import('@/features/admin/pages/AdminMembersPage').then((module) => ({ default: module.AdminMembersPage })),
)
const AdminConversationsPage = lazy(() =>
  import('@/features/admin/pages/AdminConversationsPage').then((module) => ({ default: module.AdminConversationsPage })),
)
const AdminWebhookEventsPage = lazy(() =>
  import('@/features/admin/pages/AdminWebhookEventsPage').then((module) => ({ default: module.AdminWebhookEventsPage })),
)
const LiffDebugPage = lazy(() =>
  import('@/features/admin/pages/LiffDebugPage').then((module) => ({ default: module.LiffDebugPage })),
)
const CreateReviewPage = lazy(() =>
  import('@/features/reviews/pages/CreateReviewPage').then((module) => ({ default: module.CreateReviewPage })),
)
const ErrorPage = lazy(() => import('@/features/system/pages/ErrorPage').then((module) => ({ default: module.ErrorPage })))
const NotFoundPage = lazy(() =>
  import('@/features/system/pages/NotFoundPage').then((module) => ({ default: module.NotFoundPage })),
)

const RouteFallback = () => <div className="px-4 py-8 text-sm text-text-subtle">頁面載入中...</div>

/** 在 `/` 先完成 liff.init，再 SPA 導向深連結目標，避免商品頁無法 shareTargetPicker。 */
const LiffDeepLinkNavigate = ({ to }: { to: string }) => {
  const [ready, setReady] = useState(false)

  useEffect(() => {
    let disposed = false

    void (async () => {
      try {
        const liffMod = await import('@line/liff')
        if (liffMod.default.isInClient()) {
          await ensureLiffReady()
        }
      } catch {
        // 仍導向目標頁；分享時會改走根路徑分享流程
      } finally {
        if (!disposed) {
          setReady(true)
        }
      }
    })()

    return () => {
      disposed = true
    }
  }, [to])

  if (!ready) {
    return <RouteFallback />
  }

  const qIndex = to.indexOf('?')
  const destination =
    qIndex >= 0
      ? { pathname: to.slice(0, qIndex), search: to.slice(qIndex) }
      : { pathname: to }

  return <Navigate to={destination} replace />
}

const RootEntry = () => {
  const location = useLocation()
  if (isAdminLiffDebugEntry(location.search)) {
    if (adminLiffDebugUrlNeedsCleanup(location.search)) {
      return (
        <Navigate
          to={{
            pathname: '/',
            search: buildCleanAdminLiffDebugSearch(location.search),
            hash: location.hash,
          }}
          replace
        />
      )
    }
    return <LiffDebugPage mode="liffEntry" />
  }

  if (isListingShareEntry(location.pathname, location.search)) {
    if (listingShareEntryNeedsCleanup(location.pathname, location.search)) {
      return (
        <Navigate
          to={{
            pathname: '/',
            search: buildCleanListingShareEntrySearch(location.search),
            hash: location.hash,
          }}
          replace
        />
      )
    }
    return <ListingShareLiffPage />
  }

  if (isLineNotifyBindingEntry(location.pathname, location.search)) {
    if (location.pathname !== '/') {
      return <Navigate to={{ pathname: '/', search: location.search }} replace />
    }
    return <LineNotifyLiffPage />
  }

  const target = resolveLiffEntryTarget(location.pathname, location.search)
  if (target) {
    return <LiffDeepLinkNavigate to={target} />
  }

  return <Navigate to="/listings" replace />
}

const LiffPathEntry = () => {
  const location = useLocation()
  if (isLineNotifyBindingEntry(location.pathname, location.search)) {
    return <Navigate to={{ pathname: '/', search: location.search }} replace />
  }

  const target = resolveLiffEntryTarget(location.pathname, location.search)
  if (target) {
    return <LiffDeepLinkNavigate to={target} />
  }

  return <Navigate to="/listings" replace />
}

const LineNotifyLiffCanonicalRedirect = () => {
  const { search } = useLocation()
  return <Navigate to={{ pathname: '/', search }} replace />
}

const ListingShareLiffCanonicalRedirect = () => {
  const location = useLocation()
  const params = new URLSearchParams(location.search)
  params.set('listingShare', '1')
  return <Navigate to={{ pathname: '/', search: `?${params.toString()}` }} replace />
}

export const AppRouter = () => {
  return (
    <Suspense fallback={<RouteFallback />}>
      <Routes>
        <Route element={<AppLayout />}>
          <Route path="/" element={<RootEntry />} />
          <Route path="/liff" element={<LiffPathEntry />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/auth/line/callback" element={<LineLoginCallbackPage />} />
          <Route path="/liff/line-notify" element={<LineNotifyLiffCanonicalRedirect />} />
          <Route path="/liff/share-listing" element={<ListingShareLiffCanonicalRedirect />} />
          <Route path="/terms" element={<TermsPage />} />
          <Route path="/privacy" element={<PrivacyPage />} />
          <Route path="/error" element={<ErrorPage />} />
          <Route path="/contact-admin" element={<ContactAdminPage />} />
          <Route path="/listing" element={<Navigate to="/listings" replace />} />
          <Route path="/listings" element={<ListingHomePage />} />
          <Route path="/listings/:id" element={<ListingDetailPage />} />
          <Route path="/seller/:sellerId" element={<SellerPage />} />
          <Route element={<RequireAuth />}>
            <Route element={<RequireAdmin />}>
              <Route element={<AdminLayout />}>
                <Route path="/admin" element={<AdminDashboardPage />} />
                <Route path="/admin/announcements" element={<AdminAnnouncementsPage />} />
                <Route path="/admin/listings" element={<AdminListingsPage />} />
                <Route path="/admin/members" element={<AdminMembersPage />} />
                <Route path="/admin/conversations" element={<AdminConversationsPage />} />
                <Route path="/admin/webhook-events" element={<AdminWebhookEventsPage />} />
                <Route path="/admin/liff-debug" element={<LiffDebugPage mode="admin" />} />
              </Route>
            </Route>
            <Route path="/account" element={<AccountPage />} />
            <Route path="/profile" element={<Navigate to="/account" replace />} />
            <Route path="/account/email-verify" element={<AccountEmailVerifyPage />} />
            <Route path="/notifications" element={<NotificationCenterPage />} />
            <Route path="/favorites" element={<FavoritesPage />} />
            <Route path="/my-favorites" element={<Navigate to="/favorites" replace />} />
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
