import { useEffect, useState } from 'react'
import { useSession, getApiErrorMessage } from '../../app/session'
import { mesApi } from '../../lib/api'
import { formatNumber } from '../../lib/formatters'
import type { WorkOrderListItem } from '../../types/runtime'

export function MesModulePage() {
  const { session } = useSession()
  const [workOrders, setWorkOrders] = useState<WorkOrderListItem[]>([])
  const [selectedWorkOrderId, setSelectedWorkOrderId] = useState('')
  const [reportForm, setReportForm] = useState({
    goodQuantity: '0',
    scrapQuantity: '0',
    reworkQuantity: '0',
    laborHours: '8',
    machineHours: '6',
    scrapReasonCode: 'QUALITY-HOLD',
  })
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)

  const reportableWorkOrders = workOrders.filter((workOrder) => workOrder.state === 'Started')

  useEffect(() => {
    if (reportableWorkOrders.length > 0 && !selectedWorkOrderId) {
      const firstWorkOrder = reportableWorkOrders[0]

      setSelectedWorkOrderId(firstWorkOrder.id)
      setReportForm((current) => ({
        ...current,
        goodQuantity: String(firstWorkOrder.plannedQuantity),
      }))
    }
  }, [reportableWorkOrders, selectedWorkOrderId])

  async function refreshData() {
    setIsLoading(true)

    try {
      const workOrdersResponse = await mesApi.getWorkOrders()
      setWorkOrders(workOrdersResponse)
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

  async function handleDispatch(workOrderId: string) {
    setIsSaving(true)

    try {
      await mesApi.dispatchWorkOrder(workOrderId)
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleStart(workOrderId: string) {
    setIsSaving(true)

    try {
      await mesApi.startWorkOrder(workOrderId)
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleReport() {
    if (!selectedWorkOrderId) {
      return
    }

    setIsSaving(true)

    try {
      await mesApi.reportWorkOrder(selectedWorkOrderId, {
        goodQuantity: Number(reportForm.goodQuantity),
        scrapQuantity: Number(reportForm.scrapQuantity),
        reworkQuantity: Number(reportForm.reworkQuantity),
        laborHours: Number(reportForm.laborHours),
        machineHours: Number(reportForm.machineHours),
        scrapReasonCode: reportForm.scrapReasonCode || null,
      })
      setSelectedWorkOrderId('')
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
          <span className="tag-chip tag-chip-gold">MES</span>
          <span className="tag-chip tag-chip-blue">Execution Truth</span>
        </div>
        <h1 className="hero-title">Run work orders where execution, WIP, and reporting actually happen.</h1>
        <p className="hero-copy">
          Work orders are now live operational objects. Dispatch, start, and production reporting
          update MES state and push completion status back to ERP.
        </p>
      </section>

      {error ? <div className="notice-banner notice-banner-error">{error}</div> : null}

      <section className="content-grid">
        <article className="surface-card">
          <h3 className="surface-title">Execution Rules</h3>
          <ul className="surface-list">
            <li>ERP can release demand, but MES owns execution state.</li>
            <li>Work orders distinguish machine-driven and manual operations.</li>
            <li>Reporting captures good quantity, scrap, labor, and machine hours.</li>
          </ul>
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Production Report</h3>
          <div className="form-grid">
            <label className="form-field">
              <span>Started work order</span>
              <select
                value={selectedWorkOrderId}
                onChange={(event) => setSelectedWorkOrderId(event.target.value)}
              >
                <option value="">Select a work order</option>
                {reportableWorkOrders.map((workOrder) => (
                  <option key={workOrder.id} value={workOrder.id}>
                    {workOrder.workOrderNumber} | {workOrder.productCode}
                  </option>
                ))}
              </select>
            </label>
            <label className="form-field">
              <span>Good quantity</span>
              <input
                type="number"
                value={reportForm.goodQuantity}
                onChange={(event) =>
                  setReportForm((current) => ({ ...current, goodQuantity: event.target.value }))
                }
              />
            </label>
            <label className="form-field">
              <span>Scrap quantity</span>
              <input
                type="number"
                value={reportForm.scrapQuantity}
                onChange={(event) =>
                  setReportForm((current) => ({ ...current, scrapQuantity: event.target.value }))
                }
              />
            </label>
            <label className="form-field">
              <span>Rework quantity</span>
              <input
                type="number"
                value={reportForm.reworkQuantity}
                onChange={(event) =>
                  setReportForm((current) => ({ ...current, reworkQuantity: event.target.value }))
                }
              />
            </label>
            <label className="form-field">
              <span>Labor hours</span>
              <input
                type="number"
                value={reportForm.laborHours}
                onChange={(event) =>
                  setReportForm((current) => ({ ...current, laborHours: event.target.value }))
                }
              />
            </label>
            <label className="form-field">
              <span>Machine hours</span>
              <input
                type="number"
                value={reportForm.machineHours}
                onChange={(event) =>
                  setReportForm((current) => ({ ...current, machineHours: event.target.value }))
                }
              />
            </label>
            <label className="form-field">
              <span>Scrap reason</span>
              <input
                value={reportForm.scrapReasonCode}
                onChange={(event) =>
                  setReportForm((current) => ({ ...current, scrapReasonCode: event.target.value }))
                }
              />
            </label>
          </div>
          <button className="button button-primary" disabled={isSaving || !selectedWorkOrderId} onClick={() => void handleReport()} type="button">
            Report production
          </button>
        </article>
      </section>

      <section className="surface-card">
        <h3 className="surface-title">Work Orders</h3>
        {isLoading ? (
          <p className="muted-copy">Loading work orders.</p>
        ) : (
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Work Order</th>
                  <th>Production Order</th>
                  <th>Current Operation</th>
                  <th>Issue Mode</th>
                  <th>Machine Driven</th>
                  <th>Output</th>
                  <th>State</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {workOrders.map((workOrder) => (
                  <tr key={workOrder.id}>
                    <td>
                      <strong>{workOrder.workOrderNumber}</strong>
                      <div className="cell-meta">
                        {workOrder.productCode} | {formatNumber(workOrder.plannedQuantity)} pcs
                      </div>
                    </td>
                    <td>{workOrder.productionOrderNumber}</td>
                    <td>{workOrder.currentOperationName}</td>
                    <td>{workOrder.issueMode}</td>
                    <td>{workOrder.isMachineDriven ? 'Yes' : 'No'}</td>
                    <td>
                      Good {formatNumber(workOrder.goodQuantity)} / Scrap{' '}
                      {formatNumber(workOrder.scrapQuantity)}
                    </td>
                    <td>
                      <span className="status-pill">{workOrder.state}</span>
                    </td>
                    <td>
                      <div className="action-row">
                        <button
                          className="button button-secondary"
                          disabled={isSaving || workOrder.state !== 'Created'}
                          onClick={() => void handleDispatch(workOrder.id)}
                          type="button"
                        >
                          Dispatch
                        </button>
                        <button
                          className="button button-primary"
                          disabled={isSaving || workOrder.state !== 'Dispatched'}
                          onClick={() => void handleStart(workOrder.id)}
                          type="button"
                        >
                          Start
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
    </div>
  )
}
