import { useEffect, useMemo, useState } from 'react'
import { useSession, getApiErrorMessage } from '../../app/session'
import { erpApi, integrationApi } from '../../lib/api'
import { formatCurrency, formatDateTime } from '../../lib/formatters'
import type {
  AccountingBridgeItem,
  AccountingExportBatch,
  ExternalSystemStatus,
  IntegrationJobItem,
} from '../../types/runtime'

export function IntegrationModulePage() {
  const { session } = useSession()
  const [externalSystems, setExternalSystems] = useState<ExternalSystemStatus[]>([])
  const [exportBatches, setExportBatches] = useState<AccountingExportBatch[]>([])
  const [integrationJobs, setIntegrationJobs] = useState<IntegrationJobItem[]>([])
  const [accountingBridges, setAccountingBridges] = useState<AccountingBridgeItem[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)

  const enabledSystems = externalSystems.filter((system) => system.isEnabled)
  const readyBridgeCount = useMemo(
    () => accountingBridges.filter((bridge) => bridge.state === 'ReadyForExport').length,
    [accountingBridges],
  )

  async function refreshData() {
    setIsLoading(true)

    try {
      const [systemsResponse, batchesResponse, jobsResponse, bridgesResponse] = await Promise.all([
        integrationApi.getExternalSystems(),
        integrationApi.getAccountingExportBatches(),
        integrationApi.getIntegrationJobs(),
        erpApi.getAccountingBridges(),
      ])

      setExternalSystems(systemsResponse)
      setExportBatches(batchesResponse)
      setIntegrationJobs(jobsResponse)
      setAccountingBridges(bridgesResponse)
      setError(null)
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
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

  async function handleCreateBatch(targetSystemCode: string) {
    setIsSaving(true)

    try {
      await integrationApi.createAccountingExportBatch({ targetSystemCode })
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleDispatch(batchId: string) {
    setIsSaving(true)

    try {
      await integrationApi.dispatchAccountingExportBatch(batchId)
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleReconcileJob(jobId: string) {
    setIsSaving(true)

    try {
      await integrationApi.reconcileIntegrationJob(jobId, {
        notes: 'Manual reconcile requested from integration console.',
      })
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleCompleteJob(jobId: string, outcome: 'Succeeded' | 'Failed') {
    setIsSaving(true)

    try {
      await integrationApi.completeIntegrationJob(jobId, {
        outcome,
        errorMessage: outcome === 'Failed' ? 'Simulated adapter failure from integration console.' : null,
      })
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  return (
    <div className="page-shell">
      <section className="hero-card">
        <div className="tag-row">
          <span className="tag-chip tag-chip-gold">Integration</span>
          <span className="tag-chip tag-chip-blue">Kingdee Coexistence</span>
        </div>
        <h1 className="hero-title">Batch financial exports and monitor adapter outcomes explicitly.</h1>
        <p className="hero-copy">
          Integration now turns ready accounting bridges into export batches, dispatches them
          through a configured adapter, tracks the external job reference, and reconciles the final
          acknowledgement back onto batches and bridge states.
        </p>
      </section>

      {error ? <div className="notice-banner notice-banner-error">{error}</div> : null}

      <section className="content-grid">
        <article className="surface-card">
          <h3 className="surface-title">External Systems</h3>
          {isLoading ? (
            <p className="muted-copy">Loading external-system status.</p>
          ) : (
            <div className="table-shell">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>System</th>
                    <th>Endpoint</th>
                    <th>Adapter</th>
                    <th>Status</th>
                    <th>Last Check</th>
                    <th>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {externalSystems.map((system) => (
                    <tr key={system.id}>
                      <td>
                        <strong>{system.systemCode}</strong>
                        <div className="cell-meta">{system.name}</div>
                      </td>
                      <td>{system.baseUrl}</td>
                      <td>{system.adapterMode}</td>
                      <td>{system.healthStatus}</td>
                      <td>{system.lastCheckedAt ? formatDateTime(system.lastCheckedAt) : 'Unknown'}</td>
                      <td>
                        <button
                          className="button button-primary"
                          disabled={isSaving || !system.isEnabled || readyBridgeCount === 0}
                          onClick={() => void handleCreateBatch(system.systemCode)}
                          type="button"
                        >
                          Create Export Batch
                        </button>
                        {system.lastError ? <div className="cell-meta">{system.lastError}</div> : null}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Bridge Readiness</h3>
          <div className="definition-grid">
            <div>
              <span className="definition-label">Ready Bridges</span>
              <strong>{readyBridgeCount}</strong>
            </div>
            <div>
              <span className="definition-label">Enabled Targets</span>
              <strong>{enabledSystems.length}</strong>
            </div>
            <div>
              <span className="definition-label">Open Batches</span>
              <strong>{exportBatches.filter((batch) => batch.state !== 'Acknowledged').length}</strong>
            </div>
            <div>
              <span className="definition-label">Open Jobs</span>
              <strong>{integrationJobs.filter((job) => job.state === 'Sent' || job.state === 'Queued').length}</strong>
            </div>
          </div>
        </article>
      </section>

      <section className="content-grid">
        <article className="surface-card">
          <h3 className="surface-title">Export Batches</h3>
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Batch</th>
                  <th>Target</th>
                  <th>Bridges</th>
                  <th>Total</th>
                  <th>Attempt</th>
                  <th>State</th>
                  <th>Created</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {exportBatches.map((batch) => (
                  <tr key={batch.id}>
                    <td>{batch.batchNumber}</td>
                    <td>{batch.targetSystemCode}</td>
                    <td>{batch.bridgeCount}</td>
                    <td>{formatCurrency(batch.totalAmount)}</td>
                    <td>{batch.attemptCount}</td>
                    <td>{batch.state}</td>
                    <td>{formatDateTime(batch.createdAt)}</td>
                    <td>
                      <button
                        className="button button-primary"
                        disabled={isSaving || (batch.state !== 'Prepared' && batch.state !== 'Failed')}
                        onClick={() => void handleDispatch(batch.id)}
                        type="button"
                      >
                        {batch.state === 'Failed' ? 'Retry Dispatch' : 'Dispatch'}
                      </button>
                      {batch.externalBatchNumber ? <div className="cell-meta">External batch {batch.externalBatchNumber}</div> : null}
                      {batch.lastMessage ? <div className="cell-meta">{batch.lastMessage}</div> : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Integration Jobs</h3>
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Job</th>
                  <th>Target</th>
                  <th>Batch</th>
                  <th>External Job</th>
                  <th>Payload</th>
                  <th>State</th>
                  <th>Attempt</th>
                  <th>Updated</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {integrationJobs.map((job) => (
                  <tr key={job.id}>
                    <td>{job.jobNumber}</td>
                    <td>{job.targetSystemCode}</td>
                    <td>{job.relatedBatchNumber ?? 'Not linked'}</td>
                    <td>{job.externalJobNumber ?? 'Pending external ref'}</td>
                    <td>{job.payloadSummary}</td>
                    <td>{job.state}</td>
                    <td>{job.attemptNumber}</td>
                    <td>{formatDateTime(job.lastAttemptedAt ?? job.queuedAt)}</td>
                    <td>
                      <div className="action-row">
                        <button
                          className="button button-primary"
                          disabled={isSaving || (job.state !== 'Sent' && job.state !== 'Queued')}
                          onClick={() => void handleReconcileJob(job.id)}
                          type="button"
                        >
                          Reconcile
                        </button>
                        <button
                          className="button button-secondary"
                          disabled={isSaving || (job.state !== 'Sent' && job.state !== 'Queued')}
                          onClick={() => void handleCompleteJob(job.id, 'Succeeded')}
                          type="button"
                        >
                          Override Success
                        </button>
                        <button
                          className="button button-secondary"
                          disabled={isSaving || (job.state !== 'Sent' && job.state !== 'Queued')}
                          onClick={() => void handleCompleteJob(job.id, 'Failed')}
                          type="button"
                        >
                          Override Failed
                        </button>
                      </div>
                      {job.responseCode ? <div className="cell-meta">HTTP {job.responseCode}</div> : null}
                      {job.responseSummary ? <div className="cell-meta">{job.responseSummary}</div> : null}
                      {job.lastError ? <div className="cell-meta">{job.lastError}</div> : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </article>
      </section>

      <section className="surface-card">
        <h3 className="surface-title">Accounting Bridges</h3>
        <div className="table-shell">
          <table className="data-table">
            <thead>
              <tr>
                <th>Bridge</th>
                <th>Source</th>
                <th>Account</th>
                <th>Amount</th>
                <th>State</th>
              </tr>
            </thead>
            <tbody>
              {accountingBridges.map((bridge) => (
                <tr key={bridge.id}>
                  <td>{bridge.bridgeNumber}</td>
                  <td>
                    <strong>{bridge.sourceDocumentNumber}</strong>
                    <div className="cell-meta">{bridge.sourceEvent}</div>
                  </td>
                  <td>{bridge.postingAccount}</td>
                  <td>{formatCurrency(bridge.amount)}</td>
                  <td>{bridge.state}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  )
}
