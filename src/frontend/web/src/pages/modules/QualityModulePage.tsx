import { useEffect, useState } from 'react'
import { useSession, getApiErrorMessage } from '../../app/session'
import { qualityApi } from '../../lib/api'
import { formatDate, formatNumber } from '../../lib/formatters'
import type { QualityInspectionItem } from '../../types/runtime'

export function QualityModulePage() {
  const { session } = useSession()
  const [inspections, setInspections] = useState<QualityInspectionItem[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)

  async function refreshData() {
    setIsLoading(true)

    try {
      const inspectionsResponse = await qualityApi.getInspections()
      setInspections(inspectionsResponse)
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

  async function handleDisposition(inspection: QualityInspectionItem, disposition: string) {
    setIsSaving(true)

    try {
      await qualityApi.completeInspection(inspection.id, {
        disposition,
        defectQuantity: disposition === 'Release' ? 0 : inspection.defectQuantity,
        notes: `${disposition} applied from the quality module.`,
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
          <span className="tag-chip tag-chip-gold">Quality</span>
          <span className="tag-chip tag-chip-blue">Hold / Release</span>
        </div>
        <h1 className="hero-title">Control whether finished batches can move into usable stock.</h1>
        <p className="hero-copy">
          Final inspection records are now created from MES completion reports. Quality decisions
          directly update inventory availability and determine whether production can be considered settled.
        </p>
      </section>

      {error ? <div className="notice-banner notice-banner-error">{error}</div> : null}

      <section className="surface-card">
        <h3 className="surface-title">Inspection Queue</h3>
        {isLoading ? (
          <p className="muted-copy">Loading quality inspections.</p>
        ) : (
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Inspection</th>
                  <th>Work Order</th>
                  <th>Batch</th>
                  <th>Quantity</th>
                  <th>Defects</th>
                  <th>State</th>
                  <th>Disposition</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {inspections.map((inspection) => {
                  const isOpen =
                    inspection.state === 'InInspection' ||
                    inspection.state === 'Planned' ||
                    inspection.state === 'SampleCollected'

                  return (
                    <tr key={inspection.id}>
                      <td>
                        <strong>{inspection.inspectionNumber}</strong>
                        <div className="cell-meta">{inspection.inspectionType}</div>
                      </td>
                      <td>
                        {inspection.workOrderNumber}
                        <div className="cell-meta">{inspection.productionOrderNumber}</div>
                      </td>
                      <td>{inspection.batchNumber}</td>
                      <td>{formatNumber(inspection.inspectedQuantity)}</td>
                      <td>{formatNumber(inspection.defectQuantity)}</td>
                      <td>{inspection.state}</td>
                      <td>{inspection.disposition ?? 'Pending'}</td>
                      <td>
                        {isOpen ? (
                          <div className="action-row">
                            <button
                              className="button button-primary"
                              disabled={isSaving}
                              onClick={() => void handleDisposition(inspection, 'Release')}
                              type="button"
                            >
                              Release
                            </button>
                            <button
                              className="button button-secondary"
                              disabled={isSaving}
                              onClick={() => void handleDisposition(inspection, 'ConditionalRelease')}
                              type="button"
                            >
                              Conditional
                            </button>
                            <button
                              className="button button-secondary"
                              disabled={isSaving}
                              onClick={() => void handleDisposition(inspection, 'Rework')}
                              type="button"
                            >
                              Rework
                            </button>
                            <button
                              className="button button-secondary"
                              disabled={isSaving}
                              onClick={() => void handleDisposition(inspection, 'Reject')}
                              type="button"
                            >
                              Reject
                            </button>
                          </div>
                        ) : (
                          <span className="cell-meta">
                            {inspection.completedAt ? formatDate(inspection.completedAt) : 'Completed'}
                          </span>
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}
