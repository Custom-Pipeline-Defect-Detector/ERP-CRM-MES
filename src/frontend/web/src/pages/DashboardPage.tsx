import { moduleRegistry } from '../app/moduleRegistry'

const deliveryOrder = [
  'Platform foundation, authentication, RBAC, and auditability',
  'Master data, CRM core, ERP core, and MES core',
  'Quality controls, traceability depth, and cost accounting',
  'Reporting customization, reconciliation, and AI services',
]

export function DashboardPage() {
  const phase1Modules = moduleRegistry.filter((module) => module.phase === 'Phase 1').length
  const laterModules = moduleRegistry.length - phase1Modules

  return (
    <div className="page-shell">
      <section className="hero-card">
        <span className="section-kicker">Launch Strategy</span>
        <h1 className="hero-title">
          Build the operational spine first, then add costing, governance, and AI.
        </h1>
        <p className="hero-copy">
          This scaffold is optimized for a phased factory rollout: one intranet platform, clear
          domain boundaries, source-code delivery, and a migration path that keeps Kingdee in
          place until the client is ready to retire dependencies safely.
        </p>
        <div className="tag-row">
          <span className="tag-chip tag-chip-gold">200 Users</span>
          <span className="tag-chip tag-chip-blue">Kingdee Coexistence</span>
          <span className="tag-chip tag-chip-green">Modular Monolith</span>
          <span className="tag-chip tag-chip-slate">Phase Delivery</span>
        </div>
      </section>

      <section className="stats-grid">
        <article className="stat-card">
          <span className="stat-label">Phase 1 Modules</span>
          <strong className="stat-value">{phase1Modules}</strong>
        </article>
        <article className="stat-card">
          <span className="stat-label">Later-Phase Modules</span>
          <strong className="stat-value">{laterModules}</strong>
        </article>
        <article className="stat-card">
          <span className="stat-label">Total Domains</span>
          <strong className="stat-value">{moduleRegistry.length}</strong>
        </article>
      </section>

      <section className="content-grid">
        <article className="surface-card">
          <h3 className="surface-title">Delivery Order</h3>
          <ol className="timeline-list">
            {deliveryOrder.map((item) => (
              <li key={item}>{item}</li>
            ))}
          </ol>
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Go-Live Priorities</h3>
          <ul className="surface-list">
            <li>Sales order to production order traceability</li>
            <li>Material issue to inventory and cost linkage</li>
            <li>Inspection and defect capture at key checkpoints</li>
            <li>Management dashboards and exportable reports</li>
          </ul>
        </article>
      </section>

      <section className="surface-card">
        <h3 className="surface-title">Module Map</h3>
        <div className="module-grid">
          {moduleRegistry.map((module) => (
            <article className="module-card" key={module.key}>
              <div className="module-head">
                <h4 className="module-title">{module.title}</h4>
                <span className="tag-chip tag-chip-outline">{module.phase}</span>
              </div>
              <p className="module-summary">{module.summary}</p>
              <ul className="surface-list">
                {module.deliveryFocus.map((item) => (
                  <li key={item}>{item}</li>
                ))}
              </ul>
            </article>
          ))}
        </div>
      </section>
    </div>
  )
}
