import { useEffect, useState } from 'react'
import { useSession, getApiErrorMessage } from '../../app/session'
import { platformApi } from '../../lib/api'
import { formatDate } from '../../lib/formatters'
import type { AuditLogItem, UserListItem } from '../../types/runtime'

export function PlatformModulePage() {
  const { session } = useSession()
  const [users, setUsers] = useState<UserListItem[]>([])
  const [auditLogs, setAuditLogs] = useState<AuditLogItem[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  async function refreshData() {
    setIsLoading(true)

    try {
      const [usersResponse, auditLogsResponse] = await Promise.all([
        platformApi.getUsers(),
        platformApi.getAuditLogs(16),
      ])

      setUsers(usersResponse)
      setAuditLogs(auditLogsResponse)
      setError(null)
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
      setUsers([])
      setAuditLogs([])
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    if (!session) {
      return
    }

    void refreshData()
  }, [session?.token])

  return (
    <div className="page-shell">
      <section className="hero-card">
        <div className="tag-row">
          <span className="tag-chip tag-chip-gold">Phase 1</span>
          <span className="tag-chip tag-chip-blue">Live Runtime</span>
        </div>
        <h1 className="hero-title">Platform control, access, and auditability.</h1>
        <p className="hero-copy">
          This implementation now carries real session handling, role-based access, and audit
          events across CRM, ERP, and MES workflow actions.
        </p>
      </section>

      <section className="content-grid">
        <article className="surface-card">
          <h3 className="surface-title">Current Session</h3>
          {session ? (
            <div className="definition-grid">
              <div>
                <span className="definition-label">User</span>
                <strong>{session.user.displayName}</strong>
              </div>
              <div>
                <span className="definition-label">Department</span>
                <strong>{session.user.department}</strong>
              </div>
              <div>
                <span className="definition-label">Roles</span>
                <strong>{session.user.roles.join(', ')}</strong>
              </div>
              <div>
                <span className="definition-label">Permissions</span>
                <strong>{session.user.permissions.length}</strong>
              </div>
            </div>
          ) : (
            <p className="muted-copy">Waiting for a session.</p>
          )}
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Runtime Status</h3>
          <ul className="surface-list">
            <li>Authentication uses local session tokens over the intranet API.</li>
            <li>CRM, ERP, and MES commands write audit events immediately.</li>
            <li>Permissions are enforced per action instead of per page only.</li>
          </ul>
        </article>
      </section>

      {error ? <div className="notice-banner notice-banner-error">{error}</div> : null}

      <section className="surface-card">
        <div className="section-header">
          <h3 className="surface-title">User Directory</h3>
          <button className="button button-secondary" onClick={() => void refreshData()} type="button">
            Refresh
          </button>
        </div>
        {isLoading ? (
          <p className="muted-copy">Loading platform data.</p>
        ) : (
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>User</th>
                  <th>Department</th>
                  <th>Roles</th>
                </tr>
              </thead>
              <tbody>
                {users.map((user) => (
                  <tr key={user.id}>
                    <td>
                      <strong>{user.displayName}</strong>
                      <div className="cell-meta">@{user.userName}</div>
                    </td>
                    <td>{user.department}</td>
                    <td>{user.roles.join(', ')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="surface-card">
        <h3 className="surface-title">Audit Trail</h3>
        {isLoading ? (
          <p className="muted-copy">Loading audit events.</p>
        ) : (
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Time</th>
                  <th>Module</th>
                  <th>Action</th>
                  <th>Detail</th>
                  <th>Actor</th>
                </tr>
              </thead>
              <tbody>
                {auditLogs.map((auditLog) => (
                  <tr key={auditLog.id}>
                    <td>{formatDate(auditLog.occurredAt)}</td>
                    <td>{auditLog.entityName}</td>
                    <td>{auditLog.actionName}</td>
                    <td>{auditLog.detail}</td>
                    <td>{auditLog.performedBy}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}
