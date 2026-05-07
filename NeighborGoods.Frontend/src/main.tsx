import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'
import { AuthProvider } from '@/features/auth/components/AuthProvider'
import { SharedMessageHubProvider } from '@/features/messaging/context/SharedMessageHubProvider'
import siteLogoUrl from '@/png/logo.png'
import './index.css'
import App from './App.tsx'

const applySiteIcons = (href: string) => {
  let icon = document.querySelector<HTMLLinkElement>('link[rel="icon"]')
  if (!icon) {
    icon = document.createElement('link')
    icon.rel = 'icon'
    document.head.appendChild(icon)
  }
  icon.type = 'image/png'
  icon.href = href

  let apple = document.querySelector<HTMLLinkElement>('link[rel="apple-touch-icon"]')
  if (!apple) {
    apple = document.createElement('link')
    apple.rel = 'apple-touch-icon'
    document.head.appendChild(apple)
  }
  apple.href = href
}

applySiteIcons(siteLogoUrl)

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AuthProvider>
          <SharedMessageHubProvider>
            <App />
          </SharedMessageHubProvider>
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
)
