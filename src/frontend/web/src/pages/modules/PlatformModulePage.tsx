import { type FormEvent, useEffect, useState } from 'react'
import { useSession, getApiErrorMessage } from '../../app/session'
import { platformApi } from '../../lib/api'
import { formatDate, formatDateTime } from '../../lib/formatters'
import type {
  AuditLogItem,
  DepartmentListItem,
  PasswordResetResult,
  RoleListItem,
  UserListItem,
} from '../../types/runtime'

export function PlatformModulePage() {
  const { session } = useSession()
  const [users, setUsers] = useState<UserListItem[]>([])
  const [roles, setRoles] = useState<RoleListItem[]>([])
  const [departments, setDepartments] = useState<DepartmentListItem[]>([])
  const [auditLogs, setAuditLogs] = useState<AuditLogItem[]>([])
  const [selectedUserId, setSelectedUserId] = useState('')
  const [selectedDepartmentId, setSelectedDepartmentId] = useState('')
  const [selectedRoleIds, setSelectedRoleIds] = useState<string[]>([])
  const [newDepartment, setNewDepartment] = useState({ code: '', name: '' })
  const [passwordReset, setPasswordReset] = useState<PasswordResetResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)

  const selectedUser = users.find((user) => user.id === selectedUserId) ?? users[0] ?? null
  const selectedUserRoleKey = selectedUser?.roles.join('|') ?? ''

  async function refreshData() {
    setIsLoading(true)

    try {
      const [usersResponse, rolesResponse, departmentsResponse, auditLogsResponse] = await Promise.all([
        platformApi.getUsers(),
        platformApi.getRoles(),
        platformApi.getDepartments(),
        platformApi.getAuditLogs(16),
      ])

      setUsers(usersResponse)
      setRoles(rolesResponse)
      setDepartments(departmentsResponse)
      setAuditLogs(auditLogsResponse)
      setSelectedUserId((currentUserId) =>
        usersResponse.some((user) => user.id === currentUserId) ? currentUserId : usersResponse[0]?.id ?? '',
      )
      setError(null)
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
      setUsers([])
      setRoles([])
      setDepartments([])
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

  useEffect(() => {
    if (!selectedUser) {
      setSelectedDepartmentId('')
      setSelectedRoleIds([])
      return
    }

    setSelectedDepartmentId(selectedUser.departmentId)
    setSelectedRoleIds(
      roles.filter((role) => selectedUser.roles.includes(role.name)).map((role) => role.id),
    )
  }, [selectedUser?.id, selectedUser?.departmentId, selectedUserRoleKey, roles])

  async function handleCreateDepartment(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setIsSaving(true)

    try {
      await platformApi.createDepartment(newDepartment)
      setNewDepartment({ code: '', name: '' })
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleSaveUserDepartment() {
    if (!selectedUser || !selectedDepartmentId) {
      setError('Select a user and department before saving.')
      return
    }

    setIsSaving(true)

    try {
      await platformApi.updateUserDepartment(selectedUser.id, { departmentId: selectedDepartmentId })
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleSaveUserRoles() {
    if (!selectedUser || selectedRoleIds.length === 0) {
      setError('Select a user and at least one role before saving.')
      return
    }

    setIsSaving(true)

    try {
      await platformApi.updateUserRoles(selectedUser.id, { roleIds: selectedRoleIds })
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleResetPassword(user: UserListItem) {
    setIsSaving(true)

    try {
      const resetResult = await platformApi.resetUserPassword(user.id)
      setPasswordReset(resetResult)
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleUnlockUser(user: UserListItem) {
    setIsSaving(true)

    try {
      await platformApi.unlockUser(user.id)
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  function handleRoleToggle(roleId: string, isChecked: boolean) {
    setSelectedRoleIds((currentRoleIds) =>
      isChecked
        ? Array.from(new Set([...currentRoleIds, roleId]))
        : currentRoleIds.filter((currentRoleId) => currentRoleId !== roleId),
    )
  }

  return (
    <div className="page-shell">
      <section className="hero-card">
        <div className="tag-row">
          <span className="tag-chip tag-chip-gold">Phase 1</span>
          <span className="tag-chip tag-chip-blue">Live Runtime</span>
        </div>
        <h1 className="hero-title">Platform control, access, and auditability.</h1>
        <p className="hero-copy">
          This implementation now carries password-backed session handling, account recovery,
          department ownership, role assignment, and audit events across CRM, ERP, and MES actions.
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
            <li>Authentication uses local password verification and session tokens over the intranet API.</li>
            <li>Administrators can recover accounts, move users between departments, and assign roles.</li>
            <li>Permissions are enforced per action instead of per page only.</li>
          </ul>
        </article>
      </section>

      {error ? <div className="notice-banner notice-banner-error">{error}</div> : null}

      {passwordReset ? (
        <section className="notice-banner">
          <strong>Temporary password for @{passwordReset.userName}: </strong>
          <code>{passwordReset.temporaryPassword}</code>
          <span className="cell-meta">
            {' '}
            Expires {formatDateTime(passwordReset.expiresAt)} and must be changed after login.
          </span>
        </section>
      ) : null}

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
                  <th>Account</th>
                  <th>Last Login</th>
                  <th>Actions</th>
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
                    <td>
                      <span className="status-pill">
                        {user.lockedUntil ? 'Locked' : user.mustChangePassword ? 'Password Change' : 'Active'}
                      </span>
                      <div className="cell-meta">{user.failedAccessCount} failed attempts</div>
                      <div className="cell-meta">
                        {user.passwordResetExpiresAt
                          ? `Reset expires ${formatDateTime(user.passwordResetExpiresAt)}`
                          : 'No active reset'}
                      </div>
                    </td>
                    <td>
                      {user.lastLoginAt ? formatDateTime(user.lastLoginAt) : 'Never'}
                      <div className="cell-meta">
                        {user.lockedUntil ? `Locked until ${formatDateTime(user.lockedUntil)}` : 'No lockout'}
                      </div>
                    </td>
                    <td>
                      <div className="action-row">
                        <button
                          className="button button-secondary"
                          disabled={isSaving}
                          onClick={() => void handleResetPassword(user)}
                          type="button"
                        >
                          Reset password
                        </button>
                        <button
                          className="button button-primary"
                          disabled={isSaving || (!user.lockedUntil && user.failedAccessCount === 0)}
                          onClick={() => void handleUnlockUser(user)}
                          type="button"
                        >
                          Unlock
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="content-grid">
        <article className="surface-card">
          <h3 className="surface-title">User Assignment</h3>
          {isLoading ? (
            <p className="muted-copy">Loading assignment controls.</p>
          ) : (
            <>
              <div className="form-grid">
                <label className="form-field">
                  User
                  <select value={selectedUser?.id ?? ''} onChange={(event) => setSelectedUserId(event.target.value)}>
                    {users.map((user) => (
                      <option key={user.id} value={user.id}>
                        {user.displayName} (@{user.userName})
                      </option>
                    ))}
                  </select>
                </label>
                <label className="form-field">
                  Department
                  <select
                    value={selectedDepartmentId}
                    onChange={(event) => setSelectedDepartmentId(event.target.value)}
                  >
                    {departments.map((department) => (
                      <option key={department.id} value={department.id}>
                        {department.code} - {department.name}
                      </option>
                    ))}
                  </select>
                </label>
              </div>
              <button
                className="button button-primary"
                disabled={isSaving || !selectedUser || !selectedDepartmentId}
                onClick={() => void handleSaveUserDepartment()}
                type="button"
              >
                Save department
              </button>

              <div className="role-checkbox-grid">
                {roles.map((role) => (
                  <label key={role.id} className="role-checkbox">
                    <input
                      checked={selectedRoleIds.includes(role.id)}
                      onChange={(event) => handleRoleToggle(role.id, event.target.checked)}
                      type="checkbox"
                    />
                    <span>
                      <strong>{role.name}</strong>
                      <span className="cell-meta">{role.code}</span>
                    </span>
                  </label>
                ))}
              </div>
              <button
                className="button button-primary"
                disabled={isSaving || !selectedUser || selectedRoleIds.length === 0}
                onClick={() => void handleSaveUserRoles()}
                type="button"
              >
                Save roles
              </button>
            </>
          )}
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Departments</h3>
          <form onSubmit={(event) => void handleCreateDepartment(event)}>
            <div className="form-grid">
              <label className="form-field">
                Code
                <input
                  onChange={(event) =>
                    setNewDepartment((current) => ({ ...current, code: event.target.value }))
                  }
                  placeholder="ENG"
                  value={newDepartment.code}
                />
              </label>
              <label className="form-field">
                Name
                <input
                  onChange={(event) =>
                    setNewDepartment((current) => ({ ...current, name: event.target.value }))
                  }
                  placeholder="Engineering"
                  value={newDepartment.name}
                />
              </label>
            </div>
            <button
              className="button button-primary"
              disabled={isSaving || !newDepartment.code.trim() || !newDepartment.name.trim()}
              type="submit"
            >
              Create department
            </button>
          </form>

          <div className="table-shell table-shell-spaced">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Name</th>
                  <th>Users</th>
                </tr>
              </thead>
              <tbody>
                {departments.map((department) => (
                  <tr key={department.id}>
                    <td>{department.code}</td>
                    <td>{department.name}</td>
                    <td>{department.userCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </article>
      </section>

      <section className="surface-card">
        <h3 className="surface-title">Role Coverage</h3>
        {isLoading ? (
          <p className="muted-copy">Loading roles.</p>
        ) : (
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Role</th>
                  <th>Users</th>
                  <th>Permissions</th>
                </tr>
              </thead>
              <tbody>
                {roles.map((role) => (
                  <tr key={role.id}>
                    <td>
                      <strong>{role.name}</strong>
                      <div className="cell-meta">{role.code}</div>
                    </td>
                    <td>{role.userCount}</td>
                    <td>
                      <div className="tag-row">
                        {role.permissions.map((permission) => (
                          <span key={`${role.id}-${permission}`} className="tag-chip tag-chip-outline">
                            {permission}
                          </span>
                        ))}
                      </div>
                    </td>
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
