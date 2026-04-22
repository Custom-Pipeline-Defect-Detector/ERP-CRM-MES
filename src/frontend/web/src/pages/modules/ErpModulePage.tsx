import { useEffect, useState } from 'react'
import { useSession, getApiErrorMessage } from '../../app/session'
import { erpApi } from '../../lib/api'
import { formatDate, formatNumber } from '../../lib/formatters'
import type { ProductListItem, ProductionOrderListItem } from '../../types/runtime'

export function ErpModulePage() {
  const { session } = useSession()
  const [products, setProducts] = useState<ProductListItem[]>([])
  const [productionOrders, setProductionOrders] = useState<ProductionOrderListItem[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)

  async function refreshData() {
    setIsLoading(true)

    try {
      const [productsResponse, productionOrdersResponse] = await Promise.all([
        erpApi.getProducts(),
        erpApi.getProductionOrders(),
      ])

      setProducts(productsResponse)
      setProductionOrders(productionOrdersResponse)
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

  async function handleReleaseToMes(productionOrderId: string) {
    setIsSaving(true)

    try {
      await erpApi.releaseProductionOrderToMes(productionOrderId)
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
          <span className="tag-chip tag-chip-gold">ERP</span>
          <span className="tag-chip tag-chip-blue">Planning + Stock</span>
        </div>
        <h1 className="hero-title">Translate released commercial demand into production execution.</h1>
        <p className="hero-copy">
          ERP now carries live product master data and production-order release. When planning
          releases to MES, the runtime creates a real work order instead of stopping at status text.
        </p>
      </section>

      {error ? <div className="notice-banner notice-banner-error">{error}</div> : null}

      <section className="content-grid">
        <article className="surface-card">
          <h3 className="surface-title">Product Master</h3>
          {isLoading ? (
            <p className="muted-copy">Loading product master.</p>
          ) : (
            <div className="table-shell">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Code</th>
                    <th>Name</th>
                    <th>Family</th>
                    <th>Unit</th>
                  </tr>
                </thead>
                <tbody>
                  {products.map((product) => (
                    <tr key={product.id}>
                      <td>{product.code}</td>
                      <td>{product.name}</td>
                      <td>{product.productFamily}</td>
                      <td>{product.baseUnitCode}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Release Logic</h3>
          <ul className="surface-list">
            <li>Released CRM sales orders automatically create ERP production orders.</li>
            <li>ERP planners retain authority over release into MES.</li>
            <li>MES becomes the execution truth only after ERP release occurs.</li>
          </ul>
        </article>
      </section>

      <section className="surface-card">
        <h3 className="surface-title">Production Orders</h3>
        <div className="table-shell">
          <table className="data-table">
            <thead>
              <tr>
                <th>Production Order</th>
                <th>Sales Order</th>
                <th>Product</th>
                <th>Quantity</th>
                <th>Plan</th>
                <th>State</th>
                <th>MES Link</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {productionOrders.map((productionOrder) => (
                <tr key={productionOrder.id}>
                  <td>{productionOrder.productionOrderNumber}</td>
                  <td>{productionOrder.salesOrderNumber}</td>
                  <td>{productionOrder.productCode}</td>
                  <td>{formatNumber(productionOrder.plannedQuantity)}</td>
                  <td>
                    {formatDate(productionOrder.plannedStartDate)} to{' '}
                    {formatDate(productionOrder.plannedEndDate)}
                  </td>
                  <td>
                    <span className="status-pill">{productionOrder.state}</span>
                  </td>
                  <td>{productionOrder.workOrderNumber ?? 'Pending MES release'}</td>
                  <td>
                    <button
                      className="button button-primary"
                      disabled={isSaving || productionOrder.state !== 'Approved'}
                      onClick={() => void handleReleaseToMes(productionOrder.id)}
                      type="button"
                    >
                      Release to MES
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  )
}
