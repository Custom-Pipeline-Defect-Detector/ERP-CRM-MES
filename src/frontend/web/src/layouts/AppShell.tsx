import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { useState } from 'react'
import { moduleRegistry } from '../app/moduleRegistry'
import { useSession } from '../app/session'

export function AppShell() {
  const location = useLocation()
  const { availableUsers, error, isLoading, loginAs, session } = useSession()
  const [isSwitchingUser, setIsSwitchingUser] = useState(false)
  const selectedKey = location.pathname.startsWith('/modules/')
    ? location.pathname.split('/')[2] ?? 'dashboard'
    : 'dashboard'

  async function handleLoginAs(userName: string) {
    setIsSwitchingUser(true)

    try {
      await loginAs(userName)
    } finally {
      setIsSwitchingUser(false)
    }
  }

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand-panel">
          <span className="brand-kicker">Huashenglong Solar</span>
          <h1 className="brand-title">Manufacturing Platform</h1>
          <p className="brand-copy">
            On-premise CRM, ERP, MES, Quality, Reporting, AI, and Integration scaffold.
          </p>
          <div className="tag-row">
            <span className="tag-chip tag-chip-gold">.NET 8</span>
            <span className="tag-chip tag-chip-blue">React</span>
            <span className="tag-chip tag-chip-green">On-Premise</span>
          </div>
        </div>

        <nav className="nav-menu" aria-label="Primary">
          <NavLink
            to="/"
            end
            className={({ isActive }) =>
              isActive && selectedKey === 'dashboard' ? 'nav-link nav-link-active' : 'nav-link'
            }
          >
            Program Overview
          </NavLink>

          {moduleRegistry.map((module) => (
            <NavLink
              key={module.key}
              to={`/modules/${module.key}`}
              className={({ isActive }) => (isActive ? 'nav-link nav-link-active' : 'nav-link')}
            >
              {module.title}
            </NavLink>
          ))}
        </nav>
      </aside>

      <main className="main-panel">
        <header className="app-header">
          <span className="header-kicker">Project Backbone</span>
          <div className="header-bar">
            <div>
              <h2 className="header-title">Integrated Manufacturing Management System</h2>
              <p className="header-copy">
                {session
                  ? `${session.user.displayName} | ${session.user.department} | ${session.user.roles.join(', ')}`
                  : 'Connecting to the local platform runtime'}
              </p>
            </div>

            <div className="session-panel">
              <span className="session-label">Demo login</span>
              <div className="session-switcher">
                {availableUsers.map((userName) => (
                  <button
                    key={userName}
                    className={session?.user.userName === userName ? 'button button-primary' : 'button button-secondary'}
                    disabled={isLoading || isSwitchingUser}
                    onClick={() => {
                      void handleLoginAs(userName)
                    }}
                    type="button"
                  >
                    {userName}
                  </button>
                ))}
              </div>
            </div>
          </div>
        </header>

        <section className="app-content">
          {error ? <div className="notice-banner notice-banner-error">{error}</div> : null}
          <Outlet />
        </section>
      </main>
    </div>
  )
}
