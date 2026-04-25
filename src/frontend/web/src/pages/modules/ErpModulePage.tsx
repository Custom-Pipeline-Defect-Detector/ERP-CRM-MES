import { useEffect, useMemo, useState } from 'react'
import { useSession, getApiErrorMessage } from '../../app/session'
import { crmApi, erpApi } from '../../lib/api'
import { formatCurrency, formatDate, formatDateTime, formatNumber } from '../../lib/formatters'
import type {
  AccountingPeriod,
  AccountingBridgeItem,
  AccountingVoucher,
  CostLedgerItem,
  GoodsReceiptListItem,
  InventoryBalanceItem,
  InvoiceListItem,
  MaterialListItem,
  ProductListItem,
  ProductionOrderListItem,
  PurchaseOrderListItem,
  SalesOrderListItem,
  SalesShipmentListItem,
  SettlementAgingBucket,
  SettlementApplicationItem,
  SettlementEntry,
  SupplierListItem,
  WarehouseListItem,
  WarehouseTransferListItem,
} from '../../types/runtime'

const defaultPurchaseOrderForm = {
  supplierId: '',
  materialCode: '',
  orderedQuantity: '1000',
  unitPrice: '4.50',
  warehouseCode: 'RM-01',
  expectedReceiptDate: '2026-05-05',
}

const defaultReceiptForm = {
  purchaseOrderId: '',
  receivedQuantity: '0',
  batchNumber: 'LOT-NEW-001',
}

const defaultShipmentForm = {
  salesOrderId: '',
  shippedQuantity: '0',
  warehouseCode: 'FG-01',
}

const defaultTransferForm = {
  fromWarehouseCode: 'RM-01',
  toWarehouseCode: 'WIP-01',
  itemCode: '',
  quantity: '0',
  batchNumber: '',
}

const defaultSettlementForm = {
  settlementEntryId: '',
  amount: '0',
  reference: 'BANK-REF-001',
}

function getTodayDateValue() {
  return new Date().toISOString().slice(0, 10)
}

function createDefaultInvoiceForm() {
  return {
    settlementEntryId: '',
    issuedOn: getTodayDateValue(),
  }
}

function createDefaultPeriodCloseForm() {
  return {
    accountingPeriodId: '',
    notes: 'Month-end close after bridge reconciliation and invoice review.',
  }
}

function createDefaultVoucherForm() {
  return {
    accountingBridgeIds: [] as string[],
    notes: 'Generated from posted operational bridge entries.',
  }
}

export function ErpModulePage() {
  const { session } = useSession()
  const [products, setProducts] = useState<ProductListItem[]>([])
  const [materials, setMaterials] = useState<MaterialListItem[]>([])
  const [suppliers, setSuppliers] = useState<SupplierListItem[]>([])
  const [warehouses, setWarehouses] = useState<WarehouseListItem[]>([])
  const [salesOrders, setSalesOrders] = useState<SalesOrderListItem[]>([])
  const [purchaseOrders, setPurchaseOrders] = useState<PurchaseOrderListItem[]>([])
  const [goodsReceipts, setGoodsReceipts] = useState<GoodsReceiptListItem[]>([])
  const [warehouseTransfers, setWarehouseTransfers] = useState<WarehouseTransferListItem[]>([])
  const [productionOrders, setProductionOrders] = useState<ProductionOrderListItem[]>([])
  const [salesShipments, setSalesShipments] = useState<SalesShipmentListItem[]>([])
  const [inventoryBalances, setInventoryBalances] = useState<InventoryBalanceItem[]>([])
  const [costLedgers, setCostLedgers] = useState<CostLedgerItem[]>([])
  const [settlements, setSettlements] = useState<SettlementEntry[]>([])
  const [invoices, setInvoices] = useState<InvoiceListItem[]>([])
  const [settlementApplications, setSettlementApplications] = useState<SettlementApplicationItem[]>([])
  const [settlementAging, setSettlementAging] = useState<SettlementAgingBucket[]>([])
  const [accountingPeriods, setAccountingPeriods] = useState<AccountingPeriod[]>([])
  const [accountingBridges, setAccountingBridges] = useState<AccountingBridgeItem[]>([])
  const [accountingVouchers, setAccountingVouchers] = useState<AccountingVoucher[]>([])
  const [purchaseOrderForm, setPurchaseOrderForm] = useState(defaultPurchaseOrderForm)
  const [receiptForm, setReceiptForm] = useState(defaultReceiptForm)
  const [shipmentForm, setShipmentForm] = useState(defaultShipmentForm)
  const [transferForm, setTransferForm] = useState(defaultTransferForm)
  const [settlementForm, setSettlementForm] = useState(defaultSettlementForm)
  const [invoiceForm, setInvoiceForm] = useState(createDefaultInvoiceForm)
  const [periodCloseForm, setPeriodCloseForm] = useState(createDefaultPeriodCloseForm)
  const [voucherForm, setVoucherForm] = useState(createDefaultVoucherForm)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSaving, setIsSaving] = useState(false)

  const canManageTransfers = session?.user.permissions.includes('erp.transfer.write') ?? false
  const canManageSettlements = session?.user.permissions.includes('erp.settlement.write') ?? false
  const canManageAccounting = session?.user.permissions.includes('erp.accounting.manage') ?? false
  const canViewInvoices = session?.user.permissions.includes('erp.invoice.read') ?? false
  const canManageInvoices = session?.user.permissions.includes('erp.invoice.write') ?? false
  const canCloseAccountingPeriods = session?.user.permissions.includes('erp.period.close') ?? false

  const receivablePurchaseOrders = purchaseOrders.filter((purchaseOrder) =>
    ['Approved', 'Released', 'PartiallyReceived'].includes(purchaseOrder.state),
  )
  const shippableSalesOrders = salesOrders.filter((salesOrder) =>
    ['Released', 'PartiallyFulfilled'].includes(salesOrder.state),
  )
  const openSettlements = settlements.filter((settlement) =>
    ['Open', 'PartiallySettled'].includes(settlement.state),
  )
  const invoiceableSettlements = settlements.filter(
    (settlement) => settlement.state === 'Open' && !settlement.invoiceNumber,
  )
  const closableAccountingPeriods = accountingPeriods.filter((period) => period.state === 'Open')
  const voucheredBridgeNumbers = useMemo(
    () =>
      new Set(
        accountingVouchers.flatMap((voucher) =>
          voucher.lines.map((line) => line.sourceBridgeNumber),
        ),
      ),
    [accountingVouchers],
  )
  const voucherableBridges = useMemo(
    () =>
      accountingBridges.filter(
        (bridge) =>
          ['Posted', 'Reconciled'].includes(bridge.state) &&
          bridge.amount > 0 &&
          !voucheredBridgeNumbers.has(bridge.bridgeNumber),
      ),
    [accountingBridges, voucheredBridgeNumbers],
  )

  const transferableBalances = useMemo(
    () =>
      inventoryBalances
        .filter(
          (balance) =>
            balance.warehouseCode === transferForm.fromWarehouseCode && balance.onHandQuantity > 0,
        )
        .sort((left, right) => right.onHandQuantity - left.onHandQuantity),
    [inventoryBalances, transferForm.fromWarehouseCode],
  )

  async function refreshData() {
    setIsLoading(true)

    try {
      const [
        productsResponse,
        materialsResponse,
        suppliersResponse,
        warehousesResponse,
        salesOrdersResponse,
        purchaseOrdersResponse,
        goodsReceiptsResponse,
        warehouseTransfersResponse,
        productionOrdersResponse,
        shipmentsResponse,
        inventoryBalancesResponse,
        costLedgersResponse,
        settlementsResponse,
        invoicesResponse,
        settlementApplicationsResponse,
        settlementAgingResponse,
        accountingPeriodsResponse,
        accountingBridgesResponse,
        accountingVouchersResponse,
      ] = await Promise.all([
        erpApi.getProducts(),
        erpApi.getMaterials(),
        erpApi.getSuppliers(),
        erpApi.getWarehouses(),
        crmApi.getSalesOrders(),
        erpApi.getPurchaseOrders(),
        erpApi.getGoodsReceipts(),
        erpApi.getWarehouseTransfers(),
        erpApi.getProductionOrders(),
        erpApi.getSalesShipments(),
        erpApi.getInventoryBalances(),
        erpApi.getCostLedgers(),
        erpApi.getSettlements(),
        canViewInvoices ? erpApi.getInvoices() : Promise.resolve([]),
        erpApi.getSettlementApplications(),
        erpApi.getSettlementAging(),
        canViewInvoices ? erpApi.getAccountingPeriods() : Promise.resolve([]),
        erpApi.getAccountingBridges(),
        canManageAccounting ? erpApi.getAccountingVouchers() : Promise.resolve([]),
      ])

      setProducts(productsResponse)
      setMaterials(materialsResponse)
      setSuppliers(suppliersResponse)
      setWarehouses(warehousesResponse)
      setSalesOrders(salesOrdersResponse)
      setPurchaseOrders(purchaseOrdersResponse)
      setGoodsReceipts(goodsReceiptsResponse)
      setWarehouseTransfers(warehouseTransfersResponse)
      setProductionOrders(productionOrdersResponse)
      setSalesShipments(shipmentsResponse)
      setInventoryBalances(inventoryBalancesResponse)
      setCostLedgers(costLedgersResponse)
      setSettlements(settlementsResponse)
      setInvoices(invoicesResponse)
      setSettlementApplications(settlementApplicationsResponse)
      setSettlementAging(settlementAgingResponse)
      setAccountingPeriods(accountingPeriodsResponse)
      setAccountingBridges(accountingBridgesResponse)
      setAccountingVouchers(accountingVouchersResponse)
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

  useEffect(() => {
    if (warehouses.length === 0) {
      return
    }

    if (!transferForm.fromWarehouseCode) {
      const defaultFromWarehouse = warehouses.find((warehouse) => warehouse.code === 'RM-01') ?? warehouses[0]
      setTransferForm((current) => ({
        ...current,
        fromWarehouseCode: defaultFromWarehouse.code,
      }))
    }

    if (!transferForm.toWarehouseCode || transferForm.toWarehouseCode === transferForm.fromWarehouseCode) {
      const defaultToWarehouse =
        warehouses.find(
          (warehouse) =>
            warehouse.code !== transferForm.fromWarehouseCode &&
            (warehouse.supportsWip || warehouse.code === 'WIP-01'),
        ) ??
        warehouses.find((warehouse) => warehouse.code !== transferForm.fromWarehouseCode) ??
        warehouses[0]

      if (defaultToWarehouse) {
        setTransferForm((current) => ({
          ...current,
          toWarehouseCode: defaultToWarehouse.code,
        }))
      }
    }
  }, [warehouses, transferForm.fromWarehouseCode, transferForm.toWarehouseCode])

  useEffect(() => {
    if (transferableBalances.length === 0) {
      return
    }

    const selectedBalance =
      transferableBalances.find((balance) => balance.itemCode === transferForm.itemCode) ??
      transferableBalances[0]

    setTransferForm((current) => ({
      ...current,
      itemCode: selectedBalance.itemCode,
      quantity:
        Number(current.quantity) > 0 && current.itemCode === selectedBalance.itemCode
          ? current.quantity
          : String(Math.min(Math.max(selectedBalance.onHandQuantity / 2, 1), selectedBalance.onHandQuantity)),
    }))
  }, [transferableBalances, transferForm.itemCode])

  useEffect(() => {
    if (openSettlements.length > 0 && !settlementForm.settlementEntryId) {
      const defaultSettlement = openSettlements[0]
      setSettlementForm({
        settlementEntryId: defaultSettlement.id,
        amount: String(defaultSettlement.outstandingAmount),
        reference: 'BANK-REF-001',
      })
    }
  }, [openSettlements, settlementForm.settlementEntryId])

  useEffect(() => {
    if (invoiceableSettlements.length === 0) {
      return
    }

    const selectedSettlement =
      invoiceableSettlements.find((settlement) => settlement.id === invoiceForm.settlementEntryId) ??
      invoiceableSettlements[0]

    setInvoiceForm((current) => ({
      ...current,
      settlementEntryId: selectedSettlement.id,
    }))
  }, [invoiceableSettlements, invoiceForm.settlementEntryId])

  useEffect(() => {
    if (closableAccountingPeriods.length === 0) {
      return
    }

    const selectedPeriod =
      closableAccountingPeriods.find((period) => period.id === periodCloseForm.accountingPeriodId) ??
      closableAccountingPeriods[0]

    setPeriodCloseForm((current) => ({
      ...current,
      accountingPeriodId: selectedPeriod.id,
    }))
  }, [closableAccountingPeriods, periodCloseForm.accountingPeriodId])

  useEffect(() => {
    if (voucherableBridges.length === 0 || voucherForm.accountingBridgeIds.length > 0) {
      return
    }

    setVoucherForm((current) => ({
      ...current,
      accountingBridgeIds: [voucherableBridges[0].id],
    }))
  }, [voucherableBridges, voucherForm.accountingBridgeIds.length])

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
      setReceiptForm(defaultReceiptForm)
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleCreateTransfer() {
    if (!transferForm.itemCode) {
      return
    }

    setIsSaving(true)

    try {
      await erpApi.createWarehouseTransfer({
        fromWarehouseCode: transferForm.fromWarehouseCode,
        toWarehouseCode: transferForm.toWarehouseCode,
        itemCode: transferForm.itemCode,
        quantity: Number(transferForm.quantity),
        batchNumber: transferForm.batchNumber.trim() || null,
      })
      setTransferForm((current) => ({ ...defaultTransferForm, fromWarehouseCode: current.fromWarehouseCode, toWarehouseCode: current.toWarehouseCode }))
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
      setShipmentForm(defaultShipmentForm)
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleApplySettlement() {
    if (!settlementForm.settlementEntryId) {
      return
    }

    setIsSaving(true)

    try {
      await erpApi.applySettlement(settlementForm.settlementEntryId, {
        amount: Number(settlementForm.amount),
        reference: settlementForm.reference.trim() || null,
      })
      setSettlementForm(defaultSettlementForm)
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleCreateInvoice() {
    if (!invoiceForm.settlementEntryId) {
      return
    }

    setIsSaving(true)

    try {
      await erpApi.createInvoice({
        settlementEntryId: invoiceForm.settlementEntryId,
        issuedOn: invoiceForm.issuedOn,
      })
      setInvoiceForm(createDefaultInvoiceForm())
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleIssueInvoice(invoiceId: string) {
    setIsSaving(true)

    try {
      await erpApi.issueInvoice(invoiceId)
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleCloseAccountingPeriod() {
    if (!periodCloseForm.accountingPeriodId) {
      return
    }

    setIsSaving(true)

    try {
      await erpApi.closeAccountingPeriod(periodCloseForm.accountingPeriodId, {
        notes: periodCloseForm.notes.trim() || null,
      })
      setPeriodCloseForm(createDefaultPeriodCloseForm())
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

  function handleVoucherBridgeToggle(accountingBridgeId: string, isChecked: boolean) {
    setVoucherForm((current) => ({
      ...current,
      accountingBridgeIds: isChecked
        ? Array.from(new Set([...current.accountingBridgeIds, accountingBridgeId]))
        : current.accountingBridgeIds.filter((currentBridgeId) => currentBridgeId !== accountingBridgeId),
    }))
  }

  async function handleCreateAccountingVoucher() {
    if (voucherForm.accountingBridgeIds.length === 0) {
      setError('Select at least one posted or reconciled bridge entry before creating a voucher.')
      return
    }

    setIsSaving(true)

    try {
      await erpApi.createAccountingVoucher({
        accountingBridgeIds: voucherForm.accountingBridgeIds,
        notes: voucherForm.notes.trim() || null,
      })
      setVoucherForm(createDefaultVoucherForm())
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handleApproveAccountingVoucher(accountingVoucherId: string) {
    setIsSaving(true)

    try {
      await erpApi.approveAccountingVoucher(accountingVoucherId)
      await refreshData()
    } catch (requestError) {
      setError(getApiErrorMessage(requestError))
    } finally {
      setIsSaving(false)
    }
  }

  async function handlePostAccountingVoucher(accountingVoucherId: string) {
    setIsSaving(true)

    try {
      await erpApi.postAccountingVoucher(accountingVoucherId)
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
          <span className="tag-chip tag-chip-blue">Procure / Transfer / Ship / Invoice / Close</span>
        </div>
        <h1 className="hero-title">Run stock, operational finance support, and internal logistics on the same ledger.</h1>
        <p className="hero-copy">
          ERP now handles supplier orders, goods receipts, warehouse transfers, production release,
          customer shipments, receivable and payable settlement, invoice control, aging visibility,
          accounting bridge progression, and journal-voucher control on top of persisted data.
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

        <article className="surface-card">
          <h3 className="surface-title">Warehouse Master</h3>
          {isLoading ? (
            <p className="muted-copy">Loading warehouse structure.</p>
          ) : (
            <div className="table-shell">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Warehouse</th>
                    <th>Quality</th>
                    <th>WIP</th>
                  </tr>
                </thead>
                <tbody>
                  {warehouses.map((warehouse) => (
                    <tr key={warehouse.id}>
                      <td>
                        <strong>{warehouse.code}</strong>
                        <div className="cell-meta">{warehouse.name}</div>
                      </td>
                      <td>{warehouse.isQualityControlled ? 'Controlled' : 'Standard'}</td>
                      <td>{warehouse.supportsWip ? 'Yes' : 'No'}</td>
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
              <select
                value={purchaseOrderForm.warehouseCode}
                onChange={(event) =>
                  setPurchaseOrderForm((current) => ({ ...current, warehouseCode: event.target.value }))
                }
              >
                {warehouses.map((warehouse) => (
                  <option key={warehouse.id} value={warehouse.code}>
                    {warehouse.code} | {warehouse.name}
                  </option>
                ))}
              </select>
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
          <button
            className="button button-primary"
            disabled={isSaving}
            onClick={() => void handleCreatePurchaseOrder()}
            type="button"
          >
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
          <button
            className="button button-primary"
            disabled={isSaving || !receiptForm.purchaseOrderId}
            onClick={() => void handleReceivePurchaseOrder()}
            type="button"
          >
            Receive into stock
          </button>
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Warehouse Transfer</h3>
          <div className="form-grid">
            <label className="form-field">
              <span>From warehouse</span>
              <select
                value={transferForm.fromWarehouseCode}
                onChange={(event) =>
                  setTransferForm((current) => ({
                    ...current,
                    fromWarehouseCode: event.target.value,
                    itemCode: '',
                    batchNumber: '',
                    quantity: '0',
                  }))
                }
              >
                {warehouses.map((warehouse) => (
                  <option key={warehouse.id} value={warehouse.code}>
                    {warehouse.code} | {warehouse.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="form-field">
              <span>To warehouse</span>
              <select
                value={transferForm.toWarehouseCode}
                onChange={(event) =>
                  setTransferForm((current) => ({ ...current, toWarehouseCode: event.target.value }))
                }
              >
                {warehouses
                  .filter((warehouse) => warehouse.code !== transferForm.fromWarehouseCode)
                  .map((warehouse) => (
                    <option key={warehouse.id} value={warehouse.code}>
                      {warehouse.code} | {warehouse.name}
                    </option>
                  ))}
              </select>
            </label>
            <label className="form-field">
              <span>Item</span>
              <select
                value={transferForm.itemCode}
                onChange={(event) =>
                  setTransferForm((current) => ({ ...current, itemCode: event.target.value }))
                }
              >
                <option value="">Select item</option>
                {transferableBalances.map((balance) => (
                  <option key={`${balance.itemCode}-${balance.warehouseCode}`} value={balance.itemCode}>
                    {balance.itemCode} | {balance.itemName}
                  </option>
                ))}
              </select>
            </label>
            <label className="form-field">
              <span>Quantity</span>
              <input
                type="number"
                value={transferForm.quantity}
                onChange={(event) =>
                  setTransferForm((current) => ({ ...current, quantity: event.target.value }))
                }
              />
            </label>
            <label className="form-field">
              <span>Batch override</span>
              <input
                value={transferForm.batchNumber}
                onChange={(event) =>
                  setTransferForm((current) => ({ ...current, batchNumber: event.target.value }))
                }
                placeholder="Optional"
              />
            </label>
          </div>
          <p className="muted-copy">
            Transfer preserves batch, quality status, and unit cost from the selected source lot.
          </p>
          <button
            className="button button-primary"
            disabled={
              isSaving ||
              !canManageTransfers ||
              !transferForm.itemCode ||
              Number(transferForm.quantity) <= 0 ||
              transferForm.fromWarehouseCode === transferForm.toWarehouseCode
            }
            onClick={() => void handleCreateTransfer()}
            type="button"
          >
            Execute warehouse transfer
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
          <h3 className="surface-title">Warehouse Transfers</h3>
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Transfer</th>
                  <th>Route</th>
                  <th>Item</th>
                  <th>Batch</th>
                  <th>Qty</th>
                  <th>Value</th>
                </tr>
              </thead>
              <tbody>
                {warehouseTransfers.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="muted-copy">
                      No warehouse transfers yet.
                    </td>
                  </tr>
                ) : (
                  warehouseTransfers.map((transfer) => (
                    <tr key={transfer.id}>
                      <td>
                        <strong>{transfer.transferNumber}</strong>
                        <div className="cell-meta">{formatDate(transfer.transferredAt)}</div>
                      </td>
                      <td>
                        {transfer.fromWarehouseCode} to {transfer.toWarehouseCode}
                        <div className="cell-meta">{transfer.qualityStatus}</div>
                      </td>
                      <td>
                        <strong>{transfer.itemCode}</strong>
                        <div className="cell-meta">{transfer.itemName}</div>
                      </td>
                      <td>{transfer.batchNumber}</td>
                      <td>{formatNumber(transfer.quantity)}</td>
                      <td>{formatCurrency(transfer.totalCost)}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </article>
      </section>

      <section className="content-grid">
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

        <article className="surface-card">
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
        </article>
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
              <select
                value={shipmentForm.warehouseCode}
                onChange={(event) =>
                  setShipmentForm((current) => ({ ...current, warehouseCode: event.target.value }))
                }
              >
                {warehouses.map((warehouse) => (
                  <option key={warehouse.id} value={warehouse.code}>
                    {warehouse.code} | {warehouse.name}
                  </option>
                ))}
              </select>
            </label>
          </div>
          <button
            className="button button-primary"
            disabled={isSaving || !shipmentForm.salesOrderId}
            onClick={() => void handleCreateShipment()}
            type="button"
          >
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

      <section className="surface-card">
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
      </section>

      <section className="content-grid">
        <article className="surface-card">
          <h3 className="surface-title">Receivable / Payable Settlement</h3>
          <div className="form-grid">
            <label className="form-field">
              <span>Open entry</span>
              <select
                value={settlementForm.settlementEntryId}
                onChange={(event) => {
                  const selectedSettlement = settlements.find(
                    (settlement) => settlement.id === event.target.value,
                  )

                  setSettlementForm({
                    settlementEntryId: event.target.value,
                    amount: selectedSettlement ? String(selectedSettlement.outstandingAmount) : '0',
                    reference: 'BANK-REF-001',
                  })
                }}
              >
                <option value="">Select settlement entry</option>
                {openSettlements.map((settlement) => (
                  <option key={settlement.id} value={settlement.id}>
                    {settlement.entryNumber} | {settlement.counterpartyName}
                  </option>
                ))}
              </select>
            </label>
            <label className="form-field">
              <span>Settlement amount</span>
              <input
                type="number"
                value={settlementForm.amount}
                onChange={(event) =>
                  setSettlementForm((current) => ({ ...current, amount: event.target.value }))
                }
              />
            </label>
            <label className="form-field">
              <span>Reference</span>
              <input
                value={settlementForm.reference}
                onChange={(event) =>
                  setSettlementForm((current) => ({ ...current, reference: event.target.value }))
                }
              />
            </label>
          </div>
          <button
            className="button button-primary"
            disabled={isSaving || !canManageSettlements || !settlementForm.settlementEntryId}
            onClick={() => void handleApplySettlement()}
            type="button"
          >
            Apply settlement
          </button>

          <div className="table-shell" style={{ marginTop: '1.25rem' }}>
            <table className="data-table">
              <thead>
                <tr>
                  <th>Entry</th>
                  <th>Counterparty</th>
                  <th>Source</th>
                  <th>Total</th>
                  <th>Outstanding</th>
                  <th>Due</th>
                  <th>State</th>
                </tr>
              </thead>
              <tbody>
                {settlements.map((settlement) => (
                  <tr key={settlement.id}>
                    <td>
                      <strong>{settlement.entryNumber}</strong>
                      <div className="cell-meta">{settlement.settlementType}</div>
                    </td>
                    <td>{settlement.counterpartyName}</td>
                    <td>
                      {settlement.sourceDocumentType}
                      <div className="cell-meta">{settlement.sourceDocumentNumber}</div>
                    </td>
                    <td>{formatCurrency(settlement.totalAmount)}</td>
                    <td>{formatCurrency(settlement.outstandingAmount)}</td>
                    <td>
                      {formatDate(settlement.dueDate)}
                      <div className="cell-meta">
                        {settlement.invoiceNumber
                          ? `${settlement.invoiceNumber} | ${settlement.invoiceState}`
                          : 'Invoice pending'}
                      </div>
                    </td>
                    <td>
                      <span className="status-pill">{settlement.state}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Invoice Control</h3>
          {canViewInvoices ? (
            <>
              <div className="form-grid">
                <label className="form-field">
                  <span>Settlement entry</span>
                  <select
                    value={invoiceForm.settlementEntryId}
                    onChange={(event) =>
                      setInvoiceForm((current) => ({
                        ...current,
                        settlementEntryId: event.target.value,
                      }))
                    }
                  >
                    <option value="">Select settlement entry</option>
                    {invoiceableSettlements.map((settlement) => (
                      <option key={settlement.id} value={settlement.id}>
                        {settlement.entryNumber} | {settlement.counterpartyName}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="form-field">
                  <span>Issued on</span>
                  <input
                    type="date"
                    value={invoiceForm.issuedOn}
                    onChange={(event) =>
                      setInvoiceForm((current) => ({
                        ...current,
                        issuedOn: event.target.value,
                      }))
                    }
                  />
                </label>
              </div>
              <p className="muted-copy">
                Invoices must be created before any settlement is applied. Draft invoices block
                settlement and period close until they are issued.
              </p>
              <button
                className="button button-primary"
                disabled={isSaving || !canManageInvoices || !invoiceForm.settlementEntryId}
                onClick={() => void handleCreateInvoice()}
                type="button"
              >
                Create invoice
              </button>

              <div className="table-shell" style={{ marginTop: '1.25rem' }}>
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Invoice</th>
                      <th>Counterparty</th>
                      <th>Source</th>
                      <th>Amount</th>
                      <th>Issued</th>
                      <th>State</th>
                      <th>Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {invoices.length === 0 ? (
                      <tr>
                        <td colSpan={7} className="muted-copy">
                          No invoices created yet.
                        </td>
                      </tr>
                    ) : (
                      invoices.map((invoice) => (
                        <tr key={invoice.id}>
                          <td>
                            <strong>{invoice.invoiceNumber}</strong>
                            <div className="cell-meta">{invoice.invoiceType}</div>
                          </td>
                          <td>{invoice.counterpartyName}</td>
                          <td>
                            {invoice.sourceDocumentType}
                            <div className="cell-meta">
                              {invoice.sourceDocumentNumber} | {invoice.settlementEntryNumber}
                            </div>
                          </td>
                          <td>{formatCurrency(invoice.amount)}</td>
                          <td>
                            {formatDate(invoice.issuedOn)}
                            <div className="cell-meta">{formatDateTime(invoice.createdAt)}</div>
                          </td>
                          <td>
                            <span className="status-pill">{invoice.state}</span>
                          </td>
                          <td>
                            <button
                              className="button button-primary"
                              disabled={isSaving || !canManageInvoices || invoice.state !== 'Draft'}
                              onClick={() => void handleIssueInvoice(invoice.id)}
                              type="button"
                            >
                              Issue
                            </button>
                          </td>
                        </tr>
                      ))
                    )}
                  </tbody>
                </table>
              </div>
            </>
          ) : (
            <p className="muted-copy">
              Invoice control is restricted to finance-facing roles with invoice-read access.
            </p>
          )}
        </article>
      </section>

      <section className="content-grid">
        <article className="surface-card">
          <h3 className="surface-title">Settlement Aging</h3>
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Type</th>
                  <th>Open Entries</th>
                  <th>Current</th>
                  <th>1-30</th>
                  <th>31-60</th>
                  <th>61-90</th>
                  <th>90+</th>
                  <th>Total</th>
                </tr>
              </thead>
              <tbody>
                {settlementAging.length === 0 ? (
                  <tr>
                    <td colSpan={8} className="muted-copy">
                      No open settlements are currently aging.
                    </td>
                  </tr>
                ) : (
                  settlementAging.map((bucket) => (
                    <tr key={bucket.settlementType}>
                      <td>{bucket.settlementType}</td>
                      <td>{formatNumber(bucket.openEntries)}</td>
                      <td>{formatCurrency(bucket.currentAmount)}</td>
                      <td>{formatCurrency(bucket.days1To30Amount)}</td>
                      <td>{formatCurrency(bucket.days31To60Amount)}</td>
                      <td>{formatCurrency(bucket.days61To90Amount)}</td>
                      <td>{formatCurrency(bucket.over90DaysAmount)}</td>
                      <td>{formatCurrency(bucket.totalOutstandingAmount)}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <div className="table-shell" style={{ marginTop: '1.25rem' }}>
            <table className="data-table">
              <thead>
                <tr>
                  <th>Application</th>
                  <th>Settlement</th>
                  <th>Invoice</th>
                  <th>Amount</th>
                  <th>Reference</th>
                  <th>Applied</th>
                </tr>
              </thead>
              <tbody>
                {settlementApplications.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="muted-copy">
                      No settlement applications have been posted yet.
                    </td>
                  </tr>
                ) : (
                  settlementApplications.map((application) => (
                    <tr key={application.id}>
                      <td>
                        <strong>{application.applicationNumber}</strong>
                        <div className="cell-meta">{application.counterpartyName}</div>
                      </td>
                      <td>{application.settlementEntryNumber}</td>
                      <td>{application.invoiceNumber ?? 'No invoice'}</td>
                      <td>{formatCurrency(application.amount)}</td>
                      <td>{application.reference ?? 'Manual clearing'}</td>
                      <td>
                        {formatDateTime(application.appliedAt)}
                        <div className="cell-meta">{application.appliedBy}</div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </article>

        <article className="surface-card">
          <h3 className="surface-title">Accounting Period Control</h3>
          {canViewInvoices ? (
            <>
              <div className="form-grid">
                <label className="form-field">
                  <span>Open period</span>
                  <select
                    value={periodCloseForm.accountingPeriodId}
                    onChange={(event) =>
                      setPeriodCloseForm((current) => ({
                        ...current,
                        accountingPeriodId: event.target.value,
                      }))
                    }
                  >
                    <option value="">Select accounting period</option>
                    {closableAccountingPeriods.map((period) => (
                      <option key={period.id} value={period.id}>
                        {period.periodCode} | {formatDate(period.startDate)} to {formatDate(period.endDate)}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="form-field">
                  <span>Close notes</span>
                  <input
                    value={periodCloseForm.notes}
                    onChange={(event) =>
                      setPeriodCloseForm((current) => ({
                        ...current,
                        notes: event.target.value,
                      }))
                    }
                  />
                </label>
              </div>
              <p className="muted-copy">
                Period close is blocked while draft invoices, unresolved bridges, unvouchered
                posted bridges, or unposted vouchers exist inside the period.
              </p>
              <button
                className="button button-primary"
                disabled={isSaving || !canCloseAccountingPeriods || !periodCloseForm.accountingPeriodId}
                onClick={() => void handleCloseAccountingPeriod()}
                type="button"
              >
                Close accounting period
              </button>

              <div className="table-shell" style={{ marginTop: '1.25rem' }}>
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Period</th>
                      <th>Range</th>
                      <th>State</th>
                      <th>Closed</th>
                      <th>Notes</th>
                    </tr>
                  </thead>
                  <tbody>
                    {accountingPeriods.map((period) => (
                      <tr key={period.id}>
                        <td>{period.periodCode}</td>
                        <td>
                          {formatDate(period.startDate)} to {formatDate(period.endDate)}
                        </td>
                        <td>
                          <span className="status-pill">{period.state}</span>
                        </td>
                        <td>
                          {period.closedAt ? formatDateTime(period.closedAt) : 'Open'}
                          <div className="cell-meta">{period.closedBy ?? 'Finance control pending'}</div>
                        </td>
                        <td>{period.closeNotes ?? 'No close notes'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          ) : (
            <p className="muted-copy">
              Period governance is restricted to finance-facing roles with invoice-read access.
            </p>
          )}
        </article>
      </section>

      <section className="surface-card">
        <h3 className="surface-title">Accounting Vouchers</h3>
        <p className="muted-copy">
          Finance vouchers group posted bridge entries into balanced debit and credit lines before
          period close. Each bridge can be used once.
        </p>
        {canManageAccounting ? (
          <>
            {voucherableBridges.length === 0 ? (
              <p className="muted-copy">No posted or reconciled bridge entries are available for voucher creation.</p>
            ) : (
              <>
                <div className="role-checkbox-grid">
                  {voucherableBridges.map((bridge) => (
                    <label key={bridge.id} className="role-checkbox">
                      <input
                        checked={voucherForm.accountingBridgeIds.includes(bridge.id)}
                        onChange={(event) => handleVoucherBridgeToggle(bridge.id, event.target.checked)}
                        type="checkbox"
                      />
                      <span>
                        <strong>{bridge.bridgeNumber}</strong>
                        <span className="cell-meta">
                          {bridge.postingAccount} | {formatCurrency(bridge.amount)}
                        </span>
                        <span className="cell-meta">{bridge.sourceDocumentNumber}</span>
                      </span>
                    </label>
                  ))}
                </div>
                <div className="form-grid">
                  <label className="form-field">
                    <span>Voucher notes</span>
                    <input
                      value={voucherForm.notes}
                      onChange={(event) =>
                        setVoucherForm((current) => ({
                          ...current,
                          notes: event.target.value,
                        }))
                      }
                    />
                  </label>
                </div>
                <button
                  className="button button-primary"
                  disabled={isSaving || voucherForm.accountingBridgeIds.length === 0}
                  onClick={() => void handleCreateAccountingVoucher()}
                  type="button"
                >
                  Create voucher
                </button>
              </>
            )}

            <div className="table-shell" style={{ marginTop: '1.25rem' }}>
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Voucher</th>
                    <th>Period</th>
                    <th>Totals</th>
                    <th>Lines</th>
                    <th>Status</th>
                    <th>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {accountingVouchers.length === 0 ? (
                    <tr>
                      <td colSpan={6}>No accounting vouchers created yet.</td>
                    </tr>
                  ) : (
                    accountingVouchers.map((voucher) => (
                      <tr key={voucher.id}>
                        <td>
                          <strong>{voucher.voucherNumber}</strong>
                          <div className="cell-meta">{voucher.notes ?? 'No notes'}</div>
                        </td>
                        <td>{voucher.periodCode}</td>
                        <td>
                          Dr {formatCurrency(voucher.totalDebitAmount)}
                          <div className="cell-meta">Cr {formatCurrency(voucher.totalCreditAmount)}</div>
                        </td>
                        <td>
                          {voucher.lineCount} lines
                          <div className="cell-meta">
                            {voucher.lines.slice(0, 2).map((line) => line.accountCode).join(', ')}
                          </div>
                        </td>
                        <td>
                          <span className="status-pill">{voucher.state}</span>
                          <div className="cell-meta">
                            Created {formatDateTime(voucher.createdAt)} by {voucher.createdBy}
                          </div>
                          <div className="cell-meta">
                            {voucher.postedAt
                              ? `Posted ${formatDateTime(voucher.postedAt)} by ${voucher.postedBy}`
                              : 'Posting pending'}
                          </div>
                        </td>
                        <td>
                          <div className="action-row">
                            <button
                              className="button button-secondary"
                              disabled={isSaving || voucher.state !== 'Draft'}
                              onClick={() => void handleApproveAccountingVoucher(voucher.id)}
                              type="button"
                            >
                              Approve
                            </button>
                            <button
                              className="button button-primary"
                              disabled={isSaving || voucher.state !== 'Approved'}
                              onClick={() => void handlePostAccountingVoucher(voucher.id)}
                              type="button"
                            >
                              Post
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </>
        ) : (
          <p className="muted-copy">Voucher control is restricted to finance-facing accounting roles.</p>
        )}
      </section>

      <section className="surface-card">
        <h3 className="surface-title">Accounting Bridge</h3>
        <p className="muted-copy">
          Export, posting, and reconciliation must be complete before month-end close can pass.
        </p>
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
                        disabled={isSaving || !canManageAccounting || bridge.state !== 'ReadyForExport'}
                        onClick={() => void handleAccountingBridgeState(bridge.id, 'Exported')}
                        type="button"
                      >
                        Export
                      </button>
                      <button
                        className="button button-secondary"
                        disabled={isSaving || !canManageAccounting || bridge.state !== 'Exported'}
                        onClick={() => void handleAccountingBridgeState(bridge.id, 'Posted')}
                        type="button"
                      >
                        Post
                      </button>
                      <button
                        className="button button-primary"
                        disabled={isSaving || !canManageAccounting || bridge.state !== 'Posted'}
                        onClick={() => void handleAccountingBridgeState(bridge.id, 'Reconciled')}
                        type="button"
                      >
                        Reconcile
                      </button>
                      <button
                        className="button button-secondary"
                        disabled={isSaving || !canManageAccounting || bridge.state !== 'Failed'}
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
      </section>
    </div>
  )
}
