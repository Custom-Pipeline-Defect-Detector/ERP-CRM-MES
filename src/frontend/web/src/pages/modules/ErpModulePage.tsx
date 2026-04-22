import { useEffect, useState } from 'react'
import { useSession, getApiErrorMessage } from '../../app/session'
import { crmApi, erpApi } from '../../lib/api'
import { formatCurrency, formatDate, formatNumber } from '../../lib/formatters'
import type {
  AccountingBridgeItem,
  CostLedgerItem,
  GoodsReceiptListItem,
  InventoryBalanceItem,
  MaterialListItem,
  ProductListItem,
  ProductionOrderListItem,
  PurchaseOrderListItem,
  SalesOrderListItem,
  SalesShipmentListItem,
  SupplierListItem,
} from '../../types/runtime'

const defaultPurchaseOrderForm = {
  supplierId: '',
  materialCode: '',
  orderedQuantity: '1000',
  unitPrice: '4.50',
  warehouseCode: 'RM-01',
  expectedReceiptDate: '2026-05-05',
}

export function ErpModulePage() {
  const { session } = useSession()
  const [products, setProducts] = useState<ProductListItem[]>([])
  const [materials, setMaterials] = useState<MaterialListItem[]>([])
  const [suppliers, setSuppliers] = useState<SupplierListItem[]>([])
  const [salesOrders, setSalesOrders] = useState<SalesOrderListItem[]>([])
  const [purchaseOrders, setPurchaseOrders] = useState<PurchaseOrderListItem[]>([])
  const [goodsReceipts, setGoodsReceipts] = useState<GoodsReceiptListItem[]>([])
  const [productionOrders, setProductionOrders] = useState<ProductionOrderListItem[]>([])
  const [salesShipments, setSalesShipments] = useState<SalesShipmentListItem[]>([])
  const [inventoryBalances, setInventoryBalances] = useState<InventoryBalanceItem[]>([])
  const [costLedgers, setCostLedgers] = useState<CostLedgerItem[]>([])
  const [accountingBridges, setAccountingBridges] = useState<AccountingBridgeItem[]>([])
  const [purchaseOrderForm, setPurchaseOrderForm] = useState(defaultPurchaseOrderForm)
  const [receiptForm, setReceiptForm] = useState({
    purchaseOrderId: '',
    receivedQuantity: '0',
    batchNumber: 'LOT-NEW-001',
  })
  const [shipmentForm, setShipmentForm] = useState({
    salesOrderId: '',
    shippedQuantity: '0',
    warehouseCode: 'FG-01',
  })
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)

  const receivablePurchaseOrders = purchaseOrders.filter((purchaseOrder) =>
    ['Approved', 'Released', 'PartiallyReceived'].includes(purchaseOrder.state),
  )
  const shippableSalesOrders = salesOrders.filter((salesOrder) =>
    ['Released', 'PartiallyFulfilled'].includes(salesOrder.state),
  )

  async function refreshData() {
    setIsLoading(true)

    try {
      const [
        productsResponse,
        materialsResponse,
        suppliersResponse,
        salesOrdersResponse,
        purchaseOrdersResponse,
        goodsReceiptsResponse,
        productionOrdersResponse,
        shipmentsResponse,
        inventoryBalancesResponse,
        costLedgersResponse,
        accountingBridgesResponse,
      ] = await Promise.all([
        erpApi.getProducts(),
        erpApi.getMaterials(),
        erpApi.getSuppliers(),
        crmApi.getSalesOrders(),
        erpApi.getPurchaseOrders(),
        erpApi.getGoodsReceipts(),
        erpApi.getProductionOrders(),
        erpApi.getSalesShipments(),
        erpApi.getInventoryBalances(),
        erpApi.getCostLedgers(),
        erpApi.getAccountingBridges(),
      ])

      setProducts(productsResponse)
      setMaterials(materialsResponse)
      setSuppliers(suppliersResponse)
      setSalesOrders(salesOrdersResponse)
      setPurchaseOrders(purchaseOrdersResponse)
      setGoodsReceipts(goodsReceiptsResponse)
      setProductionOrders(productionOrdersResponse)
      setSalesShipments(shipmentsResponse)
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

  useEffect(() => {
    if (suppliers.length > 0 && !purchaseOrderForm.supplierId) {
      setPurchaseOrderForm((current) => ({ ...current, supplierId: suppliers[0].id }))
    }
  }, [suppliers, purchaseOrderForm.supplierId])

  useEffect(() => {
    if (materials.length > 0 && !purchaseOrderForm.materialCode) {
      const defaultMaterial = materials[0]
      setPurchaseOrderForm((current) => ({
        ...current,
        materialCode: defaultMaterial.code,
        unitPrice: String(defaultMaterial.standardUnitCost || 1),
      }))
    }
  }, [materials, purchaseOrderForm.materialCode])

  useEffect(() => {
    if (receivablePurchaseOrders.length > 0 && !receiptForm.purchaseOrderId) {
      const defaultPurchaseOrder = receivablePurchaseOrders[0]
      const remainingQuantity = defaultPurchaseOrder.orderedQuantity - defaultPurchaseOrder.receivedQuantity

      setReceiptForm({
        purchaseOrderId: defaultPurchaseOrder.id,
        receivedQuantity: String(remainingQuantity),
        batchNumber: `LOT-${defaultPurchaseOrder.materialCode}-${defaultPurchaseOrder.orderNumber}`,
      })
    }
  }, [receivablePurchaseOrders, receiptForm.purchaseOrderId])

  useEffect(() => {
    if (shippableSalesOrders.length > 0 && !shipmentForm.salesOrderId) {
      const defaultSalesOrder = shippableSalesOrders[0]
      setShipmentForm({
        salesOrderId: defaultSalesOrder.id,
        shippedQuantity: String(defaultSalesOrder.quantity),
        warehouseCode: 'FG-01',
      })
    }
  }, [shippableSalesOrders, shipmentForm.salesOrderId])

  async function handleCreatePurchaseOrder() {
    setIsSaving(true)

    try {
      await erpApi.createPurchaseOrder({
        supplierId: purchaseOrderForm.supplierId,
        materialCode: purchaseOrderForm.materialCode,
        orderedQuantity: Number(purchaseOrderForm.orderedQuantity),
        unitPrice: Number(purchaseOrderForm.unitPrice),
        warehouseCode: purchaseOrderForm.warehouseCode,
        expectedReceiptDate: purchaseOrderForm.expectedReceiptDate,
      })
      setPurchaseOrderForm(defaultPurchaseOrderForm)
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handlePurchaseOrderAction(
    purchaseOrderId: string,
    action: 'submit' | 'approve',
  ) {
    setIsSaving(true)

    try {
      if (action === 'submit') {
        await erpApi.submitPurchaseOrder(purchaseOrderId)
      }

      if (action === 'approve') {
        await erpApi.approvePurchaseOrder(purchaseOrderId)
      }

      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleReceivePurchaseOrder() {
    if (!receiptForm.purchaseOrderId) {
      return
    }

    setIsSaving(true)

    try {
      await erpApi.receivePurchaseOrder(receiptForm.purchaseOrderId, {
        receivedQuantity: Number(receiptForm.receivedQuantity),
        batchNumber: receiptForm.batchNumber,
      })
      setReceiptForm({ purchaseOrderId: '', receivedQuantity: '0', batchNumber: 'LOT-NEW-001' })
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

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

  async function handleCreateShipment() {
    if (!shipmentForm.salesOrderId) {
      return
    }

    setIsSaving(true)

    try {
      await erpApi.createSalesShipment({
        salesOrderId: shipmentForm.salesOrderId,
        shippedQuantity: Number(shipmentForm.shippedQuantity),
        warehouseCode: shipmentForm.warehouseCode,
      })
      setShipmentForm({ salesOrderId: '', shippedQuantity: '0', warehouseCode: 'FG-01' })
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleAccountingBridgeState(accountingBridgeId: string, targetState: string) {
    setIsSaving(true)

    try {
      await erpApi.updateAccountingBridgeState(accountingBridgeId, { targetState })
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
          <span className="tag-chip tag-chip-blue">Procure / Stock / Ship / Post</span>
        </div>
        <h1 className="hero-title">Run inbound and outbound logistics with real stock and finance effects.</h1>
        <p className="hero-copy">
          ERP now handles supplier orders, goods receipts, inventory valuation, production release,
          customer shipments, and accounting bridge progression on top of persisted data.
        </p>
      </section>

      {error ? <div className="notice-banner notice-banner-error">{error}</div> : null}

      <section className="content-grid">
        <article className="surface-card">
          <h3 className="surface-title">Material Master</h3>
          {isLoading ? (
            <p className="muted-copy">Loading material master.</p>
          ) : (
            <div className="table-shell">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Material</th>
                    <th>Unit</th>
                    <th>Std Cost</th>
                    <th>Reorder</th>
                  </tr>
                </thead>
                <tbody>
                  {materials.map((material) => (
                    <tr key={material.id}>
                      <td>
                        <strong>{material.code}</strong>
                        <div className="cell-meta">{material.name}</div>
                      </td>
                      <td>{material.baseUnitCode}</td>
                      <td>{formatCurrency(material.standardUnitCost)}</td>
                      <td>{formatNumber(material.reorderLevel)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Supplier Master</h3>
          {isLoading ? (
            <p className="muted-copy">Loading supplier master.</p>
          ) : (
            <div className="table-shell">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Supplier</th>
                    <th>Category</th>
                    <th>Lead Time</th>
                    <th>Terms</th>
                  </tr>
                </thead>
                <tbody>
                  {suppliers.map((supplier) => (
                    <tr key={supplier.id}>
                      <td>
                        <strong>{supplier.code}</strong>
                        <div className="cell-meta">{supplier.name}</div>
                      </td>
                      <td>{supplier.category}</td>
                      <td>{supplier.leadTimeDays} days</td>
                      <td>{supplier.paymentTerms}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </article>
      </section>

      <section className="content-grid">
        <article className="surface-card">
          <h3 className="surface-title">Create Purchase Order</h3>
          <div className="form-grid">
            <label className="form-field">
              <span>Supplier</span>
              <select
                value={purchaseOrderForm.supplierId}
                onChange={(event) =>
                  setPurchaseOrderForm((current) => ({ ...current, supplierId: event.target.value }))
                }
              >
                {suppliers.map((supplier) => (
                  <option key={supplier.id} value={supplier.id}>
                    {supplier.code} | {supplier.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="form-field">
              <span>Material</span>
              <select
                value={purchaseOrderForm.materialCode}
                onChange={(event) =>
                  setPurchaseOrderForm((current) => ({ ...current, materialCode: event.target.value }))
                }
              >
                {materials.map((material) => (
                  <option key={material.id} value={material.code}>
                    {material.code} | {material.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="form-field">
              <span>Quantity</span>
              <input
                type="number"
                value={purchaseOrderForm.orderedQuantity}
                onChange={(event) =>
                  setPurchaseOrderForm((current) => ({ ...current, orderedQuantity: event.target.value }))
                }
              />
            </label>
            <label className="form-field">
              <span>Unit price</span>
              <input
                type="number"
                value={purchaseOrderForm.unitPrice}
                onChange={(event) =>
                  setPurchaseOrderForm((current) => ({ ...current, unitPrice: event.target.value }))
                }
              />
            </label>
            <label className="form-field">
              <span>Warehouse</span>
              <input
                value={purchaseOrderForm.warehouseCode}
                onChange={(event) =>
                  setPurchaseOrderForm((current) => ({ ...current, warehouseCode: event.target.value }))
                }
              />
            </label>
            <label className="form-field">
              <span>Expected receipt</span>
              <input
                type="date"
                value={purchaseOrderForm.expectedReceiptDate}
                onChange={(event) =>
                  setPurchaseOrderForm((current) => ({
                    ...current,
                    expectedReceiptDate: event.target.value,
                  }))
                }
              />
            </label>
          </div>
          <button className="button button-primary" disabled={isSaving} onClick={() => void handleCreatePurchaseOrder()} type="button">
            Create purchase order
          </button>
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Receive Material</h3>
          <div className="form-grid">
            <label className="form-field">
              <span>Receivable order</span>
              <select
                value={receiptForm.purchaseOrderId}
                onChange={(event) =>
                  setReceiptForm((current) => ({ ...current, purchaseOrderId: event.target.value }))
                }
              >
                <option value="">Select purchase order</option>
                {receivablePurchaseOrders.map((purchaseOrder) => (
                  <option key={purchaseOrder.id} value={purchaseOrder.id}>
                    {purchaseOrder.orderNumber} | {purchaseOrder.materialCode}
                  </option>
                ))}
              </select>
            </label>
            <label className="form-field">
              <span>Received quantity</span>
              <input
                type="number"
                value={receiptForm.receivedQuantity}
                onChange={(event) =>
                  setReceiptForm((current) => ({ ...current, receivedQuantity: event.target.value }))
                }
              />
            </label>
            <label className="form-field">
              <span>Batch number</span>
              <input
                value={receiptForm.batchNumber}
                onChange={(event) =>
                  setReceiptForm((current) => ({ ...current, batchNumber: event.target.value }))
                }
              />
            </label>
          </div>
          <button className="button button-primary" disabled={isSaving || !receiptForm.purchaseOrderId} onClick={() => void handleReceivePurchaseOrder()} type="button">
            Receive into stock
          </button>
        </article>
      </section>

      <section className="surface-card">
        <h3 className="surface-title">Purchase Orders</h3>
        <div className="table-shell">
          <table className="data-table">
            <thead>
              <tr>
                <th>Order</th>
                <th>Supplier</th>
                <th>Material</th>
                <th>Quantity</th>
                <th>Amount</th>
                <th>ETA</th>
                <th>State</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {purchaseOrders.map((purchaseOrder) => (
                <tr key={purchaseOrder.id}>
                  <td>{purchaseOrder.orderNumber}</td>
                  <td>{purchaseOrder.supplierName}</td>
                  <td>{purchaseOrder.materialCode}</td>
                  <td>
                    {formatNumber(purchaseOrder.receivedQuantity)} / {formatNumber(purchaseOrder.orderedQuantity)}
                  </td>
                  <td>{formatCurrency(purchaseOrder.totalAmount)}</td>
                  <td>{formatDate(purchaseOrder.expectedReceiptDate)}</td>
                  <td>
                    <span className="status-pill">{purchaseOrder.state}</span>
                  </td>
                  <td>
                    <div className="action-row">
                      <button
                        className="button button-secondary"
                        disabled={isSaving || purchaseOrder.state !== 'Draft'}
                        onClick={() => void handlePurchaseOrderAction(purchaseOrder.id, 'submit')}
                        type="button"
                      >
                        Submit
                      </button>
                      <button
                        className="button button-primary"
                        disabled={isSaving || purchaseOrder.state !== 'Submitted'}
                        onClick={() => void handlePurchaseOrderAction(purchaseOrder.id, 'approve')}
                        type="button"
                      >
                        Approve
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className="content-grid">
        <article className="surface-card">
          <h3 className="surface-title">Goods Receipts</h3>
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Receipt</th>
                  <th>Purchase Order</th>
                  <th>Material</th>
                  <th>Batch</th>
                  <th>Quantity</th>
                  <th>Quality</th>
                </tr>
              </thead>
              <tbody>
                {goodsReceipts.map((receipt) => (
                  <tr key={receipt.id}>
                    <td>{receipt.receiptNumber}</td>
                    <td>
                      <strong>{receipt.purchaseOrderNumber}</strong>
                      <div className="cell-meta">{receipt.supplierName}</div>
                    </td>
                    <td>{receipt.materialCode}</td>
                    <td>{receipt.batchNumber}</td>
                    <td>{formatNumber(receipt.receivedQuantity)}</td>
                    <td>{receipt.qualityStatus}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Product Master</h3>
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

      <section className="content-grid">
        <article className="surface-card">
          <h3 className="surface-title">Create Shipment</h3>
          <div className="form-grid">
            <label className="form-field">
              <span>Sales order</span>
              <select
                value={shipmentForm.salesOrderId}
                onChange={(event) =>
                  setShipmentForm((current) => ({ ...current, salesOrderId: event.target.value }))
                }
              >
                <option value="">Select sales order</option>
                {shippableSalesOrders.map((salesOrder) => (
                  <option key={salesOrder.id} value={salesOrder.id}>
                    {salesOrder.orderNumber} | {salesOrder.productCode}
                  </option>
                ))}
              </select>
            </label>
            <label className="form-field">
              <span>Shipped quantity</span>
              <input
                type="number"
                value={shipmentForm.shippedQuantity}
                onChange={(event) =>
                  setShipmentForm((current) => ({ ...current, shippedQuantity: event.target.value }))
                }
              />
            </label>
            <label className="form-field">
              <span>Warehouse</span>
              <input
                value={shipmentForm.warehouseCode}
                onChange={(event) =>
                  setShipmentForm((current) => ({ ...current, warehouseCode: event.target.value }))
                }
              />
            </label>
          </div>
          <button className="button button-primary" disabled={isSaving || !shipmentForm.salesOrderId} onClick={() => void handleCreateShipment()} type="button">
            Ship customer order
          </button>
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Sales Shipments</h3>
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Shipment</th>
                  <th>Sales Order</th>
                  <th>Customer</th>
                  <th>Product</th>
                  <th>Batch</th>
                  <th>Quantity</th>
                </tr>
              </thead>
              <tbody>
                {salesShipments.map((shipment) => (
                  <tr key={shipment.id}>
                    <td>{shipment.shipmentNumber}</td>
                    <td>{shipment.salesOrderNumber}</td>
                    <td>{shipment.customerName}</td>
                    <td>{shipment.productCode}</td>
                    <td>{shipment.batchNumber}</td>
                    <td>{formatNumber(shipment.shippedQuantity)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </article>
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
                  <th>Action</th>
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
                    <td>
                      <div className="action-row">
                        <button
                          className="button button-secondary"
                          disabled={isSaving || bridge.state !== 'ReadyForExport'}
                          onClick={() => void handleAccountingBridgeState(bridge.id, 'Exported')}
                          type="button"
                        >
                          Export
                        </button>
                        <button
                          className="button button-secondary"
                          disabled={isSaving || bridge.state !== 'Exported'}
                          onClick={() => void handleAccountingBridgeState(bridge.id, 'Posted')}
                          type="button"
                        >
                          Post
                        </button>
                        <button
                          className="button button-primary"
                          disabled={isSaving || bridge.state !== 'Posted'}
                          onClick={() => void handleAccountingBridgeState(bridge.id, 'Reconciled')}
                          type="button"
                        >
                          Reconcile
                        </button>
                        <button
                          className="button button-secondary"
                          disabled={isSaving || bridge.state !== 'Failed'}
                          onClick={() => void handleAccountingBridgeState(bridge.id, 'ReadyForExport')}
                          type="button"
                        >
                          Retry
                        </button>
                      </div>
                    </td>
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
