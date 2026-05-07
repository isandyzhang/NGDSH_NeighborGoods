import { useEffect, useRef, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { useAuth } from '@/features/auth/components/AuthProvider'
import { ADMIN_ROLE_CODE } from '@/features/admin/constants/adminRole'
import { accountApi } from '@/features/account/api/accountApi'
import siteLogo from '@/png/logo.png'
import { Button, getButtonClassName } from '@/shared/ui/Button'

const TOPNAV_DISPLAY_NAME_KEY = 'neighborGoods.topNavDisplayName'

export const TopNav = () => {
  const { pathname } = useLocation()
  const { isAuthenticated, tokens, logout } = useAuth()
  const hideLoginAction = pathname === '/login'
  const [displayName, setDisplayName] = useState<string | null>(null)
  const [menuOpen, setMenuOpen] = useState(false)
  const [loggingOut, setLoggingOut] = useState(false)
  const menuContainerRef = useRef<HTMLDivElement | null>(null)
  const logoTarget = tokens?.role === ADMIN_ROLE_CODE ? '/admin' : '/listings'

  useEffect(() => {
    if (!isAuthenticated) {
      setDisplayName(null)
      localStorage.removeItem(TOPNAV_DISPLAY_NAME_KEY)
      return
    }

    const cachedDisplayName = localStorage.getItem(TOPNAV_DISPLAY_NAME_KEY)?.trim()
    if (cachedDisplayName) {
      setDisplayName(cachedDisplayName)
    }

    let disposed = false

    void accountApi
      .me()
      .then((profile) => {
        if (!disposed) {
          const name = profile.displayName?.trim() || profile.userName?.trim()
          const resolvedName = name || '用戶'
          setDisplayName(resolvedName)
          localStorage.setItem(TOPNAV_DISPLAY_NAME_KEY, resolvedName)
        }
      })
      .catch(() => {
        if (!disposed) {
          // 若有快取名稱就沿用，避免刷新時短暫顯示「用戶」。
          setDisplayName((current) => current ?? '用戶')
        }
      })

    return () => {
      disposed = true
    }
  }, [isAuthenticated, tokens?.userId])

  useEffect(() => {
    if (!menuOpen) {
      return
    }

    const handleOutsideClick = (event: MouseEvent) => {
      if (!menuContainerRef.current?.contains(event.target as Node)) {
        setMenuOpen(false)
      }
    }

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setMenuOpen(false)
      }
    }

    document.addEventListener('mousedown', handleOutsideClick)
    document.addEventListener('keydown', handleEscape)
    return () => {
      document.removeEventListener('mousedown', handleOutsideClick)
      document.removeEventListener('keydown', handleEscape)
    }
  }, [menuOpen])

  useEffect(() => {
    setMenuOpen(false)
  }, [pathname])

  const handleLogout = async () => {
    if (loggingOut) {
      return
    }

    setLoggingOut(true)
    try {
      await logout()
      setMenuOpen(false)
      // 完整重新載入以清乾淨登入狀態與快取，並導向登入頁
      window.location.replace('/login')
    } finally {
      setLoggingOut(false)
    }
  }

  const menuItems = [
    { to: '/messages', label: '我的訊息' },
    { to: '/account', label: '我的帳號' },
    { to: '/favorites', label: '我的收藏' },
    { to: '/my-listings', label: '我的商品' },
    { to: '/listings/create', label: '刊登商品' },
    { to: '/contact-admin', label: '聯絡管理員' },
    { to: '/terms', label: '使用條款' },
    { to: '/privacy', label: '隱私條款' },
  ]

  return (
    <header className="sticky top-0 z-[1000] border-b border-border bg-bg/90 backdrop-blur">
      <div ref={menuContainerRef} className="mx-auto w-full max-w-6xl px-4">
        <div className="flex h-20 items-center justify-between">
          <Link
            to={logoTarget}
            aria-label="NeighborGoods 首頁"
            className="inline-flex items-center gap-2.5 text-[1.4rem] font-semibold tracking-tight text-text-subtle md:gap-3 md:text-[1.75rem]"
          >
            <img src={siteLogo} alt="" width={40} height={40} className="h-9 w-9 shrink-0 object-contain md:h-11 md:w-11" />
            <span>NeighborGoods</span>
          </Link>
          {hideLoginAction ? null : isAuthenticated ? (
            <>
              <Button
                type="button"
                variant="secondary"
                onClick={() => setMenuOpen((current) => !current)}
                className="h-10 max-w-[8.5rem] px-2.5 !text-sm font-semibold sm:h-11 sm:max-w-[10.5rem] sm:px-3 sm:!text-base md:h-12 md:max-w-none md:min-w-[10.5rem] md:px-4 md:!text-[1.35rem]"
                aria-expanded={menuOpen}
                aria-controls="topnav-user-menu"
              >
                <span className="block truncate">{`Hi, ${displayName ?? '用戶'}`}</span>
              </Button>
            </>
          ) : (
            <div className="flex items-center">
              <Link
                to="/login"
                className={getButtonClassName({
                  variant: 'secondary',
                  className: 'inline-flex items-center justify-center rounded-2xl px-6 py-3 text-xl font-semibold',
                })}
              >
                登入
              </Link>
            </div>
          )}
        </div>

        {hideLoginAction || !isAuthenticated ? null : (
          <div
            id="topnav-user-menu"
            className={`overflow-hidden transition-[max-height,opacity,padding] duration-300 ease-out ${
              menuOpen ? 'max-h-[48vh] border-t border-border pb-1 opacity-100' : 'max-h-0 pb-0 opacity-0'
            }`}
          >
            <div className="grid max-h-[48vh] grid-cols-2 content-start gap-2 overflow-y-auto py-2 md:grid-cols-4">
              {menuItems.map((item, index) => (
                <Link
                  key={item.to}
                  to={item.to}
                  onClick={() => setMenuOpen(false)}
                  className={getButtonClassName({
                    variant: 'secondary',
                    className: `topnav-menu-item inline-flex items-center justify-center px-3 py-3 text-xl font-semibold ${
                      menuOpen ? 'is-open' : ''
                    }`,
                  })}
                  style={menuOpen ? { animationDelay: `${index * 55}ms` } : undefined}
                >
                  {item.label}
                </Link>
              ))}
              <Button
                type="button"
                variant="secondary"
                onClick={() => void handleLogout()}
                className={`topnav-menu-item col-span-2 border border-[#E9B4B4] bg-[#FDE2E2] px-3 py-3 text-xl font-semibold text-[#B23A3A] hover:bg-[#F8D1D1] md:col-span-4 ${
                  menuOpen ? 'is-open' : ''
                }`}
                style={menuOpen ? { animationDelay: `${menuItems.length * 55}ms` } : undefined}
              >
                {loggingOut ? '登出中...' : '登出'}
              </Button>
            </div>
          </div>
        )}
      </div>
    </header>
  )
}
