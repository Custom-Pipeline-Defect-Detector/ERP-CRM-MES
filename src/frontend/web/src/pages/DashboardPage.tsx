import { useEffect, useState } from 'react'
import { useSession, getApiErrorMessage } from '../app/session'
import { reportingApi } from '../lib/api'
import { formatCurrency, formatDate, formatNumber } from '../lib/formatters'
import { moduleRegistry } from '../app/moduleRegistry'
import type { DashboardSnapshot } from '../types/runtime'

const deliveryOrder = [
  'Platform foundation, authentication, RBAC, and auditability',
  'Master data, CRM core, ERP core, and MES core',
  'Quality controls, traceability depth, and cost accounting',
  'Reporting customization, reconciliation, and AI services',
]

export function DashboardPage() {
  const { session } = useSession()
  const [dashboard, setDashboard] = useState<DashboardSnapshot | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const phase1Modules = moduleRegistry.filter((module) => module.phase === 'Phase 1').length
  const laterModules = moduleRegistry.length - phase1Modules

  useEffect(() => {
    if (!session) {
      return
    }

    let isCancelled = false

    async function loadDashboard() {
      setIsLoading(true)

      try {
        const response = await reportingApi.getDashboard()

        if (!isCancelled) {
          setDashboard(response)
          setError(null)
        }
      } catch (requestError) {
        if (!isCancelled) {
          setError(getApiErrorMessage(requestError))
        }
      } finally {
        if (!isCancelled) {
          setIsLoading(false)
        }
      }
    }

    void loadDashboard()

    return () => {
      isCancelled = true
    }
  }, [session?.token])

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

      {error ? <div className="notice-banner notice-banner-error">{error}</div> : null}

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
        <article className="stat-card">
          <span className="stat-label">Active Sales Orders</span>
          <strong className="stat-value">{dashboard?.metrics.activeSalesOrders ?? 0}</strong>
        </article>
        <article className="stat-card">
          <span className="stat-label">Active Production Orders</span>
          <strong className="stat-value">{dashboard?.metrics.activeProductionOrders ?? 0}</strong>
        </article>
        <article className="stat-card">
          <span className="stat-label">Open Sales Amount</span>
          <strong className="stat-value stat-value-currency">
            {dashboard ? formatCurrency(dashboard.metrics.openSalesAmount) : formatCurrency(0)}
          </strong>
        </article>
        <article className="stat-card">
          <span className="stat-label">Pending Inspections</span>
          <strong className="stat-value">{dashboard?.metrics.pendingQualityInspections ?? 0}</strong>
        </article>
        <article className="stat-card">
          <span className="stat-label">Inventory Items</span>
          <strong className="stat-value">{dashboard?.metrics.inventoryItemsTracked ?? 0}</strong>
        </article>
        <article className="stat-card">
          <span className="stat-label">Ready Bridge Entries</span>
          <strong className="stat-value">{dashboard?.metrics.readyAccountingBridgeEntries ?? 0}</strong>
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
          <h3 className="surface-title">Live Runtime Metrics</h3>
          {isLoading || !dashboard ? (
            <p className="muted-copy">Loading operational metrics.</p>
          ) : (
            <ul className="surface-list">
              <li>{dashboard.metrics.customerCount} customers in the shared master file</li>
              <li>{dashboard.metrics.activeWorkOrders} active work orders across the shop floor</li>
              <li>{formatNumber(dashboard.metrics.reportedGoodQuantity)} good units reported so far</li>
              <li>{dashboard.metrics.pendingQualityInspections} quality decisions are still pending</li>
            </ul>
          )}
        </article>
      </section>

      <section className="surface-card">
        <h3 className="surface-title">Recent Sales and Production</h3>
        {isLoading || !dashboard ? (
          <p className="muted-copy">Loading runtime data.</p>
        ) : (
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Sales Order</th>
                  <th>Customer</th>
                  <th>Amount</th>
                  <th>State</th>
                  <th>Production</th>
                </tr>
              </thead>
              <tbody>
                {dashboard.recentSalesOrders.map((salesOrder) => (
                  <tr key={salesOrder.id}>
                    <td>
                      <strong>{salesOrder.orderNumber}</strong>
                      <div className="cell-meta">{formatDate(salesOrder.orderDate)}</div>
                    </td>
                    <td>{salesOrder.customerName}</td>
                    <td>{formatCurrency(salesOrder.totalAmount)}</td>
                    <td>{salesOrder.state}</td>
                    <td>{salesOrder.productionOrderNumber ?? 'Pending'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="content-grid">
        <article className="surface-card">
          <h3 className="surface-title">Active Production Orders</h3>
          {isLoading || !dashboard ? (
            <p className="muted-copy">Loading production orders.</p>
          ) : (
            <ul className="surface-list">
              {dashboard.activeProductionOrders.map((productionOrder) => (
                <li key={productionOrder.id}>
                  <strong>{productionOrder.productionOrderNumber}</strong> |{' '}
                  {productionOrder.productCode} | {productionOrder.state}
                </li>
              ))}
            </ul>
          )}
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Recent Activity</h3>
          {isLoading || !dashboard ? (
            <p className="muted-copy">Loading activity feed.</p>
          ) : (
            <ul className="surface-list">
              {dashboard.recentActivity.map((activity) => (
                <li key={`${activity.occurredAt}-${activity.summary}`}>
                  <strong>{activity.module}</strong> | {activity.summary}
                </li>
              ))}
            </ul>
          )}
        </article>
      </section>
    </div>
  )
}
