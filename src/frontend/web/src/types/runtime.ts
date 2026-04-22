export interface SessionUser {
  id: string
  userName: string
  displayName: string
  department: string
  roles: string[]
  permissions: string[]
}

export interface Session {
  token: string
  expiresAt: string
  user: SessionUser
}

export interface UserListItem {
  id: string
  userName: string
  displayName: string
  department: string
  roles: string[]
}

export interface AuditLogItem {
  id: string
  occurredAt: string
  entityName: string
  actionName: string
  detail: string
  performedBy: string
}

export interface DashboardMetrics {
  customerCount: number
  activeSalesOrders: number
  activeProductionOrders: number
  activeWorkOrders: number
  openSalesAmount: number
  reportedGoodQuantity: number
  pendingQualityInspections: number
  inventoryItemsTracked: number
  readyAccountingBridgeEntries: number
}

export interface ActivityFeedItem {
  occurredAt: string
  module: string
  summary: string
}

export interface CustomerListItem {
  id: string
  code: string
  name: string
  status: string
  segment: string
  region: string
  industry: string
  creditRating: string
}

export interface ProductListItem {
  id: string
  code: string
  name: string
  productFamily: string
  baseUnitCode: string
}

export interface MaterialListItem {
  id: string
  code: string
  name: string
  baseUnitCode: string
  standardUnitCost: number
  reorderLevel: number
}

export interface SupplierListItem {
  id: string
  code: string
  name: string
  category: string
  taxRegistrationNumber: string
  leadTimeDays: number
  paymentTerms: string
}

export interface PurchaseOrderListItem {
  id: string
  orderNumber: string
  supplierId: string
  supplierName: string
  materialCode: string
  orderedQuantity: number
  receivedQuantity: number
  unitPrice: number
  totalAmount: number
  warehouseCode: string
  state: string
  orderedOn: string
  expectedReceiptDate: string
}

export interface GoodsReceiptListItem {
  id: string
  receiptNumber: string
  purchaseOrderNumber: string
  supplierName: string
  materialCode: string
  receivedQuantity: number
  warehouseCode: string
  qualityStatus: string
  state: string
  batchNumber: string
  receivedOn: string
}

export interface SalesOrderListItem {
  id: string
  orderNumber: string
  customerId: string
  customerName: string
  productCode: string
  quantity: number
  unitPrice: number
  totalAmount: number
  state: string
  orderDate: string
  requestedDeliveryDate: string
  productionOrderNumber: string | null
}

export interface SalesShipmentListItem {
  id: string
  shipmentNumber: string
  salesOrderNumber: string
  customerName: string
  productCode: string
  shippedQuantity: number
  warehouseCode: string
  batchNumber: string
  state: string
  shippedOn: string
}

export interface ProductionOrderListItem {
  id: string
  productionOrderNumber: string
  salesOrderNumber: string
  productCode: string
  plannedQuantity: number
  state: string
  plannedStartDate: string
  plannedEndDate: string
  workOrderNumber: string | null
}

export interface WorkOrderListItem {
  id: string
  workOrderNumber: string
  productionOrderNumber: string
  productCode: string
  plannedQuantity: number
  goodQuantity: number
  scrapQuantity: number
  state: string
  currentOperationName: string
  issueMode: string
  isMachineDriven: boolean
}

export interface DashboardSnapshot {
  metrics: DashboardMetrics
  recentSalesOrders: SalesOrderListItem[]
  activeProductionOrders: ProductionOrderListItem[]
  activeWorkOrders: WorkOrderListItem[]
  recentActivity: ActivityFeedItem[]
}

export interface InventoryBalanceItem {
  itemCode: string
  itemName: string
  warehouseCode: string
  onHandQuantity: number
  pendingInspectionQuantity: number
  releasedQuantity: number
  averageUnitCost: number
}

export interface CostLedgerItem {
  workOrderId: string
  workOrderNumber: string
  productionOrderNumber: string
  materialCost: number
  laborCost: number
  machineCost: number
  overheadCost: number
  scrapCost: number
  totalCost: number
  unitCost: number
}

export interface AccountingBridgeItem {
  id: string
  bridgeNumber: string
  sourceDocumentNumber: string
  sourceEvent: string
  amount: number
  postingAccount: string
  state: string
  generatedAt: string
}

export interface QualityInspectionItem {
  id: string
  inspectionNumber: string
  inspectionType: string
  workOrderNumber: string
  productionOrderNumber: string
  batchNumber: string
  inspectedQuantity: number
  defectQuantity: number
  state: string
  disposition: string | null
  inspector: string | null
  createdAt: string
  completedAt: string | null
  notes: string | null
}

export interface AiManagementSummary {
  source: string
  model: string
  generatedAt: string
  summary: string
  warnings: string[]
}

export interface ExternalSystemStatus {
  id: string
  systemCode: string
  name: string
  baseUrl: string
  isEnabled: boolean
  healthStatus: string
  lastCheckedAt: string | null
}

export interface AccountingExportBatch {
  id: string
  batchNumber: string
  targetSystemCode: string
  state: string
  bridgeCount: number
  totalAmount: number
  createdAt: string
  sentAt: string | null
  completedAt: string | null
}

export interface IntegrationJobItem {
  id: string
  jobNumber: string
  targetSystemCode: string
  jobType: string
  state: string
  relatedBatchNumber: string | null
  payloadSummary: string
  queuedAt: string
  completedAt: string | null
  lastError: string | null
}
