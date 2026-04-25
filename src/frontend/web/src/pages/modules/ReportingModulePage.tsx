import { useEffect, useState } from 'react'
import { useSession, getApiErrorMessage } from '../../app/session'
import { reportingApi } from '../../lib/api'
import { formatDateTime } from '../../lib/formatters'
import type { ReportCatalogItem, ReportExportJob, ReportPreview } from '../../types/runtime'

export function ReportingModulePage() {
  const { session } = useSession()
  const [catalog, setCatalog] = useState<ReportCatalogItem[]>([])
  const [selectedReportCode, setSelectedReportCode] = useState<string | null>(null)
  const [preview, setPreview] = useState<ReportPreview | null>(null)
  const [exportJobs, setExportJobs] = useState<ReportExportJob[]>([])
  const [selectedExportJobId, setSelectedExportJobId] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)

  const selectedExportJob = exportJobs.find((job) => job.id === selectedExportJobId) ?? exportJobs[0] ?? null

  async function refreshModule(nextReportCode?: string | null) {
    setIsLoading(true)

    try {
      const [catalogResponse, exportResponse] = await Promise.all([
        reportingApi.getCatalog(),
        reportingApi.getExportJobs(12),
      ])

      const activeReportCode = nextReportCode ?? selectedReportCode ?? catalogResponse[0]?.reportCode ?? null
      const previewResponse = activeReportCode
        ? await reportingApi.getReportPreview(activeReportCode, 20)
        : null

      setCatalog(catalogResponse)
      setSelectedReportCode(activeReportCode)
      setPreview(previewResponse)
      setExportJobs(exportResponse)
      setSelectedExportJobId((current) => current ?? exportResponse[0]?.id ?? null)
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

    void refreshModule()
  }, [session?.token])

  async function handleSelectReport(reportCode: string) {
    setSelectedReportCode(reportCode)
    await refreshModule(reportCode)
  }

  async function handleExport(reportCode: string) {
    setIsSaving(true)

    try {
      const exportJob = await reportingApi.createExportJob({ reportCode, format: 'csv' })
      setSelectedExportJobId(exportJob.id)
      await refreshModule(reportCode)
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
          <span className="tag-chip tag-chip-gold">Reporting</span>
          <span className="tag-chip tag-chip-blue">Governed Datasets</span>
        </div>
        <h1 className="hero-title">Preview operational reports and export controlled datasets.</h1>
        <p className="hero-copy">
          Reporting now runs on governed datasets derived from live CRM, ERP, MES, Quality, and
          Integration transactions. The same report definitions drive preview and export output.
        </p>
      </section>

      {error ? <div className="notice-banner notice-banner-error">{error}</div> : null}

      <section className="content-grid">
        <article className="surface-card">
          <div className="section-header">
            <h3 className="surface-title">Report Catalog</h3>
            <button className="button button-secondary" onClick={() => void refreshModule()} type="button">
              Refresh
            </button>
          </div>
          {isLoading ? (
            <p className="muted-copy">Loading report definitions.</p>
          ) : (
            <div className="table-shell">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Report</th>
                    <th>Owner</th>
                    <th>Description</th>
                    <th>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {catalog.map((item) => (
                    <tr key={item.reportCode}>
                      <td>
                        <strong>{item.name}</strong>
                        <div className="cell-meta">{item.reportCode}</div>
                      </td>
                      <td>{item.ownerModule}</td>
                      <td>{item.description}</td>
                      <td>
                        <div className="action-row">
                          <button
                            className="button button-secondary"
                            disabled={isLoading || selectedReportCode === item.reportCode}
                            onClick={() => void handleSelectReport(item.reportCode)}
                            type="button"
                          >
                            Preview
                          </button>
                          <button
                            className="button button-primary"
                            disabled={isSaving}
                            onClick={() => void handleExport(item.reportCode)}
                            type="button"
                          >
                            Export CSV
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Preview Summary</h3>
          {isLoading || !preview ? (
            <p className="muted-copy">Loading report preview.</p>
          ) : (
            <div className="definition-grid">
              <div>
                <span className="definition-label">Report</span>
                <strong>{preview.name}</strong>
              </div>
              <div>
                <span className="definition-label">Rows</span>
                <strong>{preview.totalRows}</strong>
              </div>
              <div>
                <span className="definition-label">Preview Size</span>
                <strong>{preview.rows.length}</strong>
              </div>
              <div>
                <span className="definition-label">Generated</span>
                <strong>{formatDateTime(preview.generatedAt)}</strong>
              </div>
            </div>
          )}
        </article>
      </section>

      <section className="surface-card">
        <h3 className="surface-title">Report Preview</h3>
        {isLoading || !preview ? (
          <p className="muted-copy">Loading report rows.</p>
        ) : (
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  {preview.columns.map((column) => (
                    <th key={column}>{column}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {preview.rows.map((row, rowIndex) => (
                  <tr key={`${preview.reportCode}-${rowIndex}`}>
                    {row.map((cell, cellIndex) => (
                      <td key={`${preview.reportCode}-${rowIndex}-${cellIndex}`}>{cell}</td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="content-grid">
        <article className="surface-card">
          <h3 className="surface-title">Export Jobs</h3>
          {isLoading ? (
            <p className="muted-copy">Loading export jobs.</p>
          ) : (
            <div className="table-shell">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Job</th>
                    <th>Report</th>
                    <th>Rows</th>
                    <th>State</th>
                    <th>Requested</th>
                    <th>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {exportJobs.map((job) => (
                    <tr key={job.id}>
                      <td>
                        <strong>{job.jobNumber}</strong>
                        <div className="cell-meta">{job.fileName}</div>
                      </td>
                      <td>{job.reportName}</td>
                      <td>{job.rowCount}</td>
                      <td>{job.state}</td>
                      <td>{formatDateTime(job.requestedAt)}</td>
                      <td>
                        <button
                          className="button button-secondary"
                          disabled={selectedExportJobId === job.id}
                          onClick={() => setSelectedExportJobId(job.id)}
                          type="button"
                        >
                          View Content
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Export Content</h3>
          {!selectedExportJob ? (
            <p className="muted-copy">No export job has been generated yet.</p>
          ) : (
            <>
              <div className="definition-grid">
                <div>
                  <span className="definition-label">File</span>
                  <strong>{selectedExportJob.fileName}</strong>
                </div>
                <div>
                  <span className="definition-label">Format</span>
                  <strong>{selectedExportJob.format}</strong>
                </div>
                <div>
                  <span className="definition-label">Completed</span>
                  <strong>{selectedExportJob.completedAt ? formatDateTime(selectedExportJob.completedAt) : 'Pending'}</strong>
                </div>
              </div>
              <pre className="code-block-shell">
                <code>{selectedExportJob.content}</code>
              </pre>
            </>
          )}
        </article>
      </section>
    </div>
  )
}
