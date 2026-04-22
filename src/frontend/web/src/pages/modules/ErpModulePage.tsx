import { useEffect, useState } from 'react'
import { useSession, getApiErrorMessage } from '../../app/session'
import { erpApi } from '../../lib/api'
import { formatCurrency, formatDate, formatNumber } from '../../lib/formatters'
import type {
  AccountingBridgeItem,
  CostLedgerItem,
  InventoryBalanceItem,
  ProductListItem,
  ProductionOrderListItem,
} from '../../types/runtime'

export function ErpModulePage() {
  const { session } = useSession()
  const [products, setProducts] = useState<ProductListItem[]>([])
  const [productionOrders, setProductionOrders] = useState<ProductionOrderListItem[]>([])
  const [inventoryBalances, setInventoryBalances] = useState<InventoryBalanceItem[]>([])
  const [costLedgers, setCostLedgers] = useState<CostLedgerItem[]>([])
  const [accountingBridges, setAccountingBridges] = useState<AccountingBridgeItem[]>([])
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)

  async function refreshData() {
    setIsLoading(true)

    try {
      const [
        productsResponse,
        productionOrdersResponse,
        inventoryBalancesResponse,
        costLedgersResponse,
        accountingBridgesResponse,
      ] = await Promise.all([
        erpApi.getProducts(),
        erpApi.getProductionOrders(),
        erpApi.getInventoryBalances(),
        erpApi.getCostLedgers(),
        erpApi.getAccountingBridges(),
      ])

      setProducts(productsResponse)
      setProductionOrders(productionOrdersResponse)
      setInventoryBalances(inventoryBalancesResponse)
      setCostLedgers(costLedgersResponse)
      setAccountingBridges(accountingBridgesResponse)
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
          <span className="tag-chip tag-chip-blue">Persistence + Posting</span>
        </div>
        <h1 className="hero-title">Plan production, value stock, and prepare the finance bridge.</h1>
        <p className="hero-copy">
          ERP is now backed by real persistence and holds product master, inventory movements, work
          order cost rollups, and voucher-ready bridge entries instead of placeholders.
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
            <li>Released CRM sales orders create ERP production orders.</li>
            <li>ERP planners still decide when MES receives executable demand.</li>
            <li>Inventory, costing, and accounting bridge records are generated from execution results.</li>
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
                    {formatDate(productionOrder.plannedStartDate)} to {formatDate(productionOrder.plannedEndDate)}
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

      <section className="surface-card">
        <h3 className="surface-title">Inventory Balances</h3>
        {isLoading ? (
          <p className="muted-copy">Loading inventory balances.</p>
        ) : (
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Item</th>
                  <th>Warehouse</th>
                  <th>On Hand</th>
                  <th>Pending Inspection</th>
                  <th>Released</th>
                  <th>Avg Cost</th>
                </tr>
              </thead>
              <tbody>
                {inventoryBalances.map((balance) => (
                  <tr key={`${balance.itemCode}-${balance.warehouseCode}`}>
                    <td>
                      <strong>{balance.itemCode}</strong>
                      <div className="cell-meta">{balance.itemName}</div>
                    </td>
                    <td>{balance.warehouseCode}</td>
                    <td>{formatNumber(balance.onHandQuantity)}</td>
                    <td>{formatNumber(balance.pendingInspectionQuantity)}</td>
                    <td>{formatNumber(balance.releasedQuantity)}</td>
                    <td>{formatCurrency(balance.averageUnitCost)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="content-grid">
        <article className="surface-card">
          <h3 className="surface-title">Work Order Cost Ledgers</h3>
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Work Order</th>
                  <th>Material</th>
                  <th>Labor</th>
                  <th>Machine</th>
                  <th>Overhead</th>
                  <th>Scrap</th>
                  <th>Total</th>
                </tr>
              </thead>
              <tbody>
                {costLedgers.map((costLedger) => (
                  <tr key={costLedger.workOrderId}>
                    <td>
                      <strong>{costLedger.workOrderNumber}</strong>
                      <div className="cell-meta">{costLedger.productionOrderNumber}</div>
                    </td>
                    <td>{formatCurrency(costLedger.materialCost)}</td>
                    <td>{formatCurrency(costLedger.laborCost)}</td>
                    <td>{formatCurrency(costLedger.machineCost)}</td>
                    <td>{formatCurrency(costLedger.overheadCost)}</td>
                    <td>{formatCurrency(costLedger.scrapCost)}</td>
                    <td>{formatCurrency(costLedger.totalCost)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Accounting Bridge</h3>
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
        </article>
      </section>
    </div>
  )
}
