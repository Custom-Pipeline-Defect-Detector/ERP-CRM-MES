export interface SessionUser {
  id: string
  userName: string
  displayName: string
  department: string
  mustChangePassword: boolean
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
  departmentId: string
  department: string
  mustChangePassword: boolean
  passwordResetExpiresAt: string | null
  failedAccessCount: number
  lockedUntil: string | null
  lastLoginAt: string | null
  roles: string[]
}

export interface DepartmentListItem {
  id: string
  code: string
  name: string
  userCount: number
}

export interface RoleListItem {
  id: string
  code: string
  name: string
  userCount: number
  permissions: string[]
}

export interface PasswordResetResult {
  userId: string
  userName: string
  temporaryPassword: string
  mustChangePassword: boolean
  expiresAt: string
}

export interface PasswordPolicy {
  minimumLength: number
  requireUppercase: boolean
  requireLowercase: boolean
  requireDigit: boolean
  requireSymbol: boolean
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

export interface WarehouseListItem {
  id: string
  code: string
  name: string
  isQualityControlled: boolean
  supportsWip: boolean
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

export interface WarehouseTransferListItem {
  id: string
  transferNumber: string
  fromWarehouseCode: string
  toWarehouseCode: string
  itemCode: string
  itemName: string
  quantity: number
  batchNumber: string
  qualityStatus: string
  state: string
  unitCost: number
  totalCost: number
  transferredAt: string
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

export interface ReportCatalogItem {
  reportCode: string
  name: string
  ownerModule: string
  description: string
  defaultTake: number
}

export interface ReportPreview {
  reportCode: string
  name: string
  generatedAt: string
  columns: string[]
  rows: string[][]
  totalRows: number
}

export interface ReportExportJob {
  id: string
  jobNumber: string
  reportCode: string
  reportName: string
  format: string
  state: string
  rowCount: number
  requestedBy: string
  requestedAt: string
  completedAt: string | null
  fileName: string
  content: string
  errorMessage: string | null
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

export interface SettlementEntry {
  id: string
  entryNumber: string
  settlementType: string
  sourceDocumentType: string
  sourceDocumentNumber: string
  counterpartyName: string
  totalAmount: number
  settledAmount: number
  outstandingAmount: number
  dueDate: string
  state: string
  invoiceNumber: string | null
  invoiceState: string | null
  createdAt: string
  lastSettledAt: string | null
}

export interface InvoiceListItem {
  id: string
  invoiceNumber: string
  invoiceType: string
  sourceDocumentType: string
  sourceDocumentNumber: string
  counterpartyName: string
  settlementEntryNumber: string
  amount: number
  issuedOn: string
  dueDate: string
  state: string
  createdAt: string
}

export interface SettlementApplicationItem {
  id: string
  applicationNumber: string
  settlementEntryNumber: string
  counterpartyName: string
  invoiceNumber: string | null
  amount: number
  reference: string | null
  appliedBy: string
  appliedAt: string
}

export interface SettlementAgingBucket {
  settlementType: string
  openEntries: number
  currentAmount: number
  days1To30Amount: number
  days31To60Amount: number
  days61To90Amount: number
  over90DaysAmount: number
  totalOutstandingAmount: number
}

export interface AccountingPeriod {
  id: string
  periodCode: string
  startDate: string
  endDate: string
  state: string
  closedBy: string | null
  closedAt: string | null
  closeNotes: string | null
}

export interface AccountingVoucherLine {
  id: string
  lineNumber: number
  sourceBridgeNumber: string
  sourceDocumentNumber: string
  sourceEvent: string
  accountCode: string
  debitAmount: number
  creditAmount: number
  narrative: string
}

export interface AccountingVoucher {
  id: string
  voucherNumber: string
  periodCode: string
  state: string
  totalDebitAmount: number
  totalCreditAmount: number
  lineCount: number
  createdBy: string
  createdAt: string
  approvedBy: string | null
  approvedAt: string | null
  postedBy: string | null
  postedAt: string | null
  notes: string | null
  lines: AccountingVoucherLine[]
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
  adapterMode: string
  isEnabled: boolean
  healthStatus: string
  lastCheckedAt: string | null
  lastError: string | null
}

export interface AccountingExportBatch {
  id: string
  batchNumber: string
  targetSystemCode: string
  state: string
  bridgeCount: number
  totalAmount: number
  attemptCount: number
  externalBatchNumber: string | null
  lastMessage: string | null
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
  attemptNumber: number
  relatedBatchNumber: string | null
  externalJobNumber: string | null
  externalBatchNumber: string | null
  payloadSummary: string
  responseCode: string | null
  responseSummary: string | null
  queuedAt: string
  lastAttemptedAt: string | null
  completedAt: string | null
  lastError: string | null
}
