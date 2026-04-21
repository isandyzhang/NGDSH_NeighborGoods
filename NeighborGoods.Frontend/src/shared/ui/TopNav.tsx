import { useEffect, useState } from 'react'
import { accountApi } from '@/features/account/api/accountApi'
import { NavLink } from 'react-router-dom'
import { useAuth } from '@/features/auth/components/AuthProvider'
import { Button } from './Button'

const navClass = ({ isActive }: { isActive: boolean }) =>
  `rounded-lg px-3 py-2 text-sm transition ${isActive ? 'bg-surface text-text-main' : 'text-text-subtle hover:text-text-main'}`

export const TopNav = () => {
  const { isAuthenticated, logout } = useAuth()
  const [menuOpen, setMenuOpen] = useState(false)
  const [displayName, setDisplayName] = useState<string | null>(null)

  useEffect(() => {
    if (!isAuthenticated) {
      setDisplayName(null)
      return
    }

    void accountApi
      .me()
      .then((me) => {
        setDisplayName(me.displayName)
      })
      .catch(() => {
        setDisplayName(null)
      })
  }, [isAuthenticated])

  return (
    <header className="sticky top-0 z-10 border-b border-border bg-bg/90 backdrop-blur">
      <div className="mx-auto flex h-16 w-full max-w-6xl items-center justify-between px-4">
        <div className="text-lg font-semibold text-text-main">NeighborGoods</div>
        <button
          type="button"
          className="rounded-lg border border-border px-3 py-2 text-sm text-text-main md:hidden"
          onClick={() => setMenuOpen((open) => !open)}
        >
          選單
        </button>
        <nav className="hidden items-center gap-2 md:flex">
          {isAuthenticated && displayName ? (
            <span className="mr-2 text-sm text-text-subtle">您好，{displayName}</span>
          ) : null}
          <NavLink to="/listings" className={navClass}>
            商品列表
          </NavLink>
          {isAuthenticated ? (
            <>
              <NavLink to="/messages" className={navClass}>
                訊息
              </NavLink>
              <Button variant="secondary" onClick={() => void logout()}>
                登出
              </Button>
            </>
          ) : (
            <NavLink to="/login" className={navClass}>
              登入
            </NavLink>
          )}
        </nav>
      </div>
      <div className={`border-t border-border px-4 py-3 md:hidden ${menuOpen ? 'block' : 'hidden'}`}>
        <div className="flex flex-col gap-2">
          <NavLink to="/listings" className={navClass} onClick={() => setMenuOpen(false)}>
            商品列表
          </NavLink>
          {isAuthenticated ? (
            <>
              <NavLink to="/messages" className={navClass} onClick={() => setMenuOpen(false)}>
                訊息
              </NavLink>
              {displayName ? <p className="px-3 text-sm text-text-subtle">您好，{displayName}</p> : null}
              <Button
                variant="secondary"
                onClick={() => {
                  setMenuOpen(false)
                  void logout()
                }}
              >
                登出
              </Button>
            </>
          ) : (
            <NavLink to="/login" className={navClass} onClick={() => setMenuOpen(false)}>
              登入
            </NavLink>
          )}
        </div>
      </div>
    </header>
  )
}
