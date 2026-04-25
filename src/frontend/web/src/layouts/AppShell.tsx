import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { useState, type FormEvent } from 'react'
import { moduleRegistry } from '../app/moduleRegistry'
import { useSession } from '../app/session'

export function AppShell() {
  const location = useLocation()
  const { availableUsers, changePassword, error, isLoading, login, logout, session } = useSession()
  const [loginForm, setLoginForm] = useState({ userName: 'admin', password: '' })
  const [passwordForm, setPasswordForm] = useState({ currentPassword: '', newPassword: '' })
  const [isSubmitting, setIsSubmitting] = useState(false)
  const selectedKey = location.pathname.startsWith('/modules/')
    ? location.pathname.split('/')[2] ?? 'dashboard'
    : 'dashboard'

  async function handleLoginSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setIsSubmitting(true)

    try {
      await login(loginForm.userName, loginForm.password)
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleChangePasswordSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setIsSubmitting(true)

    try {
      await changePassword(passwordForm.currentPassword, passwordForm.newPassword)
      setPasswordForm({ currentPassword: '', newPassword: '' })
    } finally {
      setIsSubmitting(false)
    }
  }

  if (!session) {
    return (
      <div className="auth-shell">
        <section className="auth-panel">
          <span className="brand-kicker">Huashenglong Solar</span>
          <h1 className="brand-title">Manufacturing Platform</h1>
          <form className="auth-form" onSubmit={(event) => void handleLoginSubmit(event)}>
            <label className="form-field">
              <span>User name</span>
              <input
                autoComplete="username"
                value={loginForm.userName}
                onChange={(event) => setLoginForm((current) => ({ ...current, userName: event.target.value }))}
              />
            </label>
            <label className="form-field">
              <span>Password</span>
              <input
                autoComplete="current-password"
                type="password"
                value={loginForm.password}
                onChange={(event) => setLoginForm((current) => ({ ...current, password: event.target.value }))}
              />
            </label>

            {error ? <div className="notice-banner notice-banner-error">{error}</div> : null}

            <button
              className="button button-primary"
              disabled={isLoading || isSubmitting || !loginForm.userName || !loginForm.password}
              type="submit"
            >
              Sign in
            </button>
          </form>

          <div className="session-panel auth-users">
            <span className="session-label">Local users</span>
            <div className="session-switcher">
              {availableUsers.map((userName) => (
                <button
                  key={userName}
                  className={loginForm.userName === userName ? 'button button-primary' : 'button button-secondary'}
                  disabled={isLoading || isSubmitting}
                  onClick={() => setLoginForm((current) => ({ ...current, userName }))}
                  type="button"
                >
                  {userName}
                </button>
              ))}
            </div>
          </div>
        </section>
      </div>
    )
  }

  if (session.user.mustChangePassword) {
    return (
      <div className="auth-shell">
        <section className="auth-panel">
          <span className="brand-kicker">{session.user.displayName}</span>
          <h1 className="brand-title">Change Password</h1>
          <form className="auth-form" onSubmit={(event) => void handleChangePasswordSubmit(event)}>
            <label className="form-field">
              <span>Current password</span>
              <input
                autoComplete="current-password"
                type="password"
                value={passwordForm.currentPassword}
                onChange={(event) =>
                  setPasswordForm((current) => ({ ...current, currentPassword: event.target.value }))
                }
              />
            </label>
            <label className="form-field">
              <span>New password</span>
              <input
                autoComplete="new-password"
                type="password"
                value={passwordForm.newPassword}
                onChange={(event) =>
                  setPasswordForm((current) => ({ ...current, newPassword: event.target.value }))
                }
              />
            </label>

            {error ? <div className="notice-banner notice-banner-error">{error}</div> : null}

            <div className="action-row">
              <button
                className="button button-primary"
                disabled={
                  isLoading ||
                  isSubmitting ||
                  !passwordForm.currentPassword ||
                  !passwordForm.newPassword
                }
                type="submit"
              >
                Update password
              </button>
              <button className="button button-secondary" onClick={logout} type="button">
                Sign out
              </button>
            </div>
          </form>
        </section>
      </div>
    )
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
              <span className="session-label">Session</span>
              <div className="session-switcher">
                <button className="button button-primary" disabled type="button">
                  {session.user.userName}
                </button>
                <button className="button button-secondary" disabled={isLoading} onClick={logout} type="button">
                  Sign out
                </button>
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
