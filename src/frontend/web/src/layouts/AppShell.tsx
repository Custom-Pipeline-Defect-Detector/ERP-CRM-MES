import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { moduleRegistry } from '../app/moduleRegistry'

export function AppShell() {
  const location = useLocation()
  const selectedKey = location.pathname.startsWith('/modules/')
    ? location.pathname.split('/')[2] ?? 'dashboard'
    : 'dashboard'

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
          <h2 className="header-title">Integrated Manufacturing Management System</h2>
        </header>

        <section className="app-content">
          <Outlet />
        </section>
      </main>
    </div>
  )
}
