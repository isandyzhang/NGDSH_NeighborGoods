import { NavLink } from 'react-router-dom'

const tabs = [
  { label: '首頁', to: '/admin' },
  { label: '跑馬燈', to: '/admin/announcements' },
  { label: '商品列表', to: '/admin/listings' },
  { label: '會員管理', to: '/admin/members' },
  { label: '聊天室檢查', to: '/admin/conversations' },
]

export const AdminTabNav = () => {
  return (
    <div className="border-b border-border">
      <div className="mx-auto flex max-w-6xl flex-wrap gap-2 px-4 pt-2">
        {tabs.map((tab) => (
          <NavLink
            key={tab.to}
            to={tab.to}
            end={tab.to === '/admin'}
            className={({ isActive }) =>
              `rounded-t-lg border border-transparent px-3 py-2 text-sm transition ${
                isActive ? 'border-border border-b-bg bg-surface font-semibold text-text-main' : 'text-text-subtle hover:bg-surface'
              }`
            }
          >
            {tab.label}
          </NavLink>
        ))}
      </div>
    </div>
  )
}
