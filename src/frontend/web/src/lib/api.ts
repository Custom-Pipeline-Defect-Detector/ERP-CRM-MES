import type {
  AccountingPeriod,
  AccountingBridgeItem,
  AccountingExportBatch,
  AccountingVoucher,
  AiManagementSummary,
  AuditLogItem,
  CostLedgerItem,
  CustomerListItem,
  DashboardSnapshot,
  DepartmentListItem,
  ExternalSystemStatus,
  GoodsReceiptListItem,
  IntegrationJobItem,
  InventoryBalanceItem,
  InvoiceListItem,
  MaterialListItem,
  PasswordResetResult,
  PasswordPolicy,
  ReportCatalogItem,
  ReportExportJob,
  ReportPreview,
  RoleListItem,
  PurchaseOrderListItem,
  ProductListItem,
  ProductionOrderListItem,
  QualityInspectionItem,
  SalesOrderListItem,
  SalesShipmentListItem,
  SettlementAgingBucket,
  SettlementApplicationItem,
  SettlementEntry,
  Session,
  SupplierListItem,
  UserListItem,
  WarehouseListItem,
  WarehouseTransferListItem,
  WorkOrderListItem,
} from '../types/runtime'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://127.0.0.1:5099'
const SESSION_TOKEN_KEY = 'manufacturing-session-token'

interface RuntimeErrorDto {
  code: string
  message: string
}

export class ApiError extends Error {
  public readonly status: number
  public readonly code: string

  constructor(message: string, status: number, code: string) {
    super(message)
    this.status = status
    this.code = code
  }
}

function getStoredSessionToken() {
  return localStorage.getItem(SESSION_TOKEN_KEY)
}

export function setStoredSessionToken(token: string) {
  localStorage.setItem(SESSION_TOKEN_KEY, token)
}

export function clearStoredSessionToken() {
  localStorage.removeItem(SESSION_TOKEN_KEY)
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers)
  headers.set('Accept', 'application/json')

  if (init?.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  const token = getStoredSessionToken()

  if (token) {
    headers.set('X-Session-Token', token)
  }

  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers,
  })

  if (!response.ok) {
    let errorBody: RuntimeErrorDto | null = null

    try {
      errorBody = (await response.json()) as RuntimeErrorDto
    } catch {
      errorBody = null
    }

    throw new ApiError(
      errorBody?.message ?? `Request failed with status ${response.status}.`,
      response.status,
      errorBody?.code ?? 'request_failed',
    )
  }

  return (await response.json()) as T
}

export const platformApi = {
  login: (userName: string, password: string) =>
    request<Session>('/api/platform/session/login', {
      method: 'POST',
      body: JSON.stringify({ userName, password }),
    }),
  getSession: () => request<Session>('/api/platform/session'),
  changePassword: (payload: { currentPassword: string; newPassword: string }) =>
    request<Session>('/api/platform/session/change-password', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  getPasswordPolicy: () => request<PasswordPolicy>('/api/platform/password-policy'),
  getUsers: () => request<UserListItem[]>('/api/platform/users'),
  getDepartments: () => request<DepartmentListItem[]>('/api/platform/departments'),
  createDepartment: (payload: { code: string; name: string }) =>
    request<DepartmentListItem>('/api/platform/departments', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  getRoles: () => request<RoleListItem[]>('/api/platform/roles'),
  updateUserDepartment: (userId: string, payload: { departmentId: string }) =>
    request<UserListItem>(`/api/platform/users/${userId}/department`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  updateUserRoles: (userId: string, payload: { roleIds: string[] }) =>
    request<UserListItem>(`/api/platform/users/${userId}/roles`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  resetUserPassword: (userId: string) =>
    request<PasswordResetResult>(`/api/platform/users/${userId}/reset-password`, {
      method: 'POST',
    }),
  unlockUser: (userId: string) =>
    request<UserListItem>(`/api/platform/users/${userId}/unlock`, {
      method: 'POST',
    }),
  getAuditLogs: (take = 20) => request<AuditLogItem[]>(`/api/platform/audit-logs?take=${take}`),
}

export const reportingApi = {
  getDashboard: () => request<DashboardSnapshot>('/api/reporting/dashboard'),
  getCatalog: () => request<ReportCatalogItem[]>('/api/reporting/catalog'),
  getReportPreview: (reportCode: string, take = 20) =>
    request<ReportPreview>(`/api/reporting/reports/${reportCode}?take=${take}`),
  getExportJobs: (take = 20) => request<ReportExportJob[]>(`/api/reporting/export-jobs?take=${take}`),
  createExportJob: (payload: { reportCode: string; format: string }) =>
    request<ReportExportJob>('/api/reporting/export-jobs', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
}

export const crmApi = {
  getCustomers: () => request<CustomerListItem[]>('/api/crm/customers'),
  createCustomer: (payload: {
    code: string
    name: string
    segment: string
    region: string
    industry: string
    creditRating: string
  }) =>
    request<CustomerListItem>('/api/crm/customers', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  getSalesOrders: () => request<SalesOrderListItem[]>('/api/crm/sales-orders'),
  createSalesOrder: (payload: {
    customerId: string
    productCode: string
    quantity: number
    unitPrice: number
    requestedDeliveryDate: string
  }) =>
    request<SalesOrderListItem>('/api/crm/sales-orders', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  submitSalesOrder: (salesOrderId: string) =>
    request<SalesOrderListItem>(`/api/crm/sales-orders/${salesOrderId}/submit`, { method: 'POST' }),
  approveSalesOrder: (salesOrderId: string) =>
    request<SalesOrderListItem>(`/api/crm/sales-orders/${salesOrderId}/approve`, { method: 'POST' }),
  releaseSalesOrder: (salesOrderId: string) =>
    request<SalesOrderListItem>(`/api/crm/sales-orders/${salesOrderId}/release`, { method: 'POST' }),
}

export const erpApi = {
  getProducts: () => request<ProductListItem[]>('/api/erp/products'),
  getMaterials: () => request<MaterialListItem[]>('/api/erp/materials'),
  getSuppliers: () => request<SupplierListItem[]>('/api/erp/suppliers'),
  getWarehouses: () => request<WarehouseListItem[]>('/api/erp/warehouses'),
  getPurchaseOrders: () => request<PurchaseOrderListItem[]>('/api/erp/purchase-orders'),
  createPurchaseOrder: (payload: {
    supplierId: string
    materialCode: string
    orderedQuantity: number
    unitPrice: number
    warehouseCode: string
    expectedReceiptDate: string
  }) =>
    request<PurchaseOrderListItem>('/api/erp/purchase-orders', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  submitPurchaseOrder: (purchaseOrderId: string) =>
    request<PurchaseOrderListItem>(`/api/erp/purchase-orders/${purchaseOrderId}/submit`, { method: 'POST' }),
  approvePurchaseOrder: (purchaseOrderId: string) =>
    request<PurchaseOrderListItem>(`/api/erp/purchase-orders/${purchaseOrderId}/approve`, { method: 'POST' }),
  receivePurchaseOrder: (
    purchaseOrderId: string,
    payload: {
      receivedQuantity: number
      batchNumber: string
    },
  ) =>
    request<GoodsReceiptListItem>(`/api/erp/purchase-orders/${purchaseOrderId}/receive`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  getGoodsReceipts: () => request<GoodsReceiptListItem[]>('/api/erp/goods-receipts'),
  getWarehouseTransfers: () => request<WarehouseTransferListItem[]>('/api/erp/warehouse-transfers'),
  createWarehouseTransfer: (payload: {
    fromWarehouseCode: string
    toWarehouseCode: string
    itemCode: string
    quantity: number
    batchNumber: string | null
  }) =>
    request<WarehouseTransferListItem>('/api/erp/warehouse-transfers', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  getProductionOrders: () => request<ProductionOrderListItem[]>('/api/erp/production-orders'),
  getSalesShipments: () => request<SalesShipmentListItem[]>('/api/erp/sales-shipments'),
  createSalesShipment: (payload: {
    salesOrderId: string
    shippedQuantity: number
    warehouseCode: string
  }) =>
    request<SalesShipmentListItem>('/api/erp/sales-shipments', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  getInventoryBalances: () => request<InventoryBalanceItem[]>('/api/erp/inventory-balances'),
  getCostLedgers: () => request<CostLedgerItem[]>('/api/erp/cost-ledgers'),
  getAccountingBridges: () => request<AccountingBridgeItem[]>('/api/erp/accounting-bridges'),
  getInvoices: () => request<InvoiceListItem[]>('/api/erp/invoices'),
  createInvoice: (payload: { settlementEntryId: string; issuedOn: string }) =>
    request<InvoiceListItem>('/api/erp/invoices', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  issueInvoice: (invoiceId: string) =>
    request<InvoiceListItem>(`/api/erp/invoices/${invoiceId}/issue`, {
      method: 'POST',
    }),
  getSettlements: () => request<SettlementEntry[]>('/api/erp/settlements'),
  applySettlement: (
    settlementEntryId: string,
    payload: {
      amount: number
      reference: string | null
    },
  ) =>
    request<SettlementEntry>(`/api/erp/settlements/${settlementEntryId}/apply`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  getSettlementApplications: () =>
    request<SettlementApplicationItem[]>('/api/erp/settlement-applications'),
  getSettlementAging: () => request<SettlementAgingBucket[]>('/api/erp/settlement-aging'),
  getAccountingPeriods: () => request<AccountingPeriod[]>('/api/erp/accounting-periods'),
  closeAccountingPeriod: (accountingPeriodId: string, payload: { notes: string | null }) =>
    request<AccountingPeriod>(`/api/erp/accounting-periods/${accountingPeriodId}/close`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  getAccountingVouchers: () => request<AccountingVoucher[]>('/api/erp/accounting-vouchers'),
  createAccountingVoucher: (payload: { accountingBridgeIds: string[]; notes: string | null }) =>
    request<AccountingVoucher>('/api/erp/accounting-vouchers', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  approveAccountingVoucher: (accountingVoucherId: string) =>
    request<AccountingVoucher>(`/api/erp/accounting-vouchers/${accountingVoucherId}/approve`, {
      method: 'POST',
    }),
  postAccountingVoucher: (accountingVoucherId: string) =>
    request<AccountingVoucher>(`/api/erp/accounting-vouchers/${accountingVoucherId}/post`, {
      method: 'POST',
    }),
  updateAccountingBridgeState: (accountingBridgeId: string, payload: { targetState: string }) =>
    request<AccountingBridgeItem>(`/api/erp/accounting-bridges/${accountingBridgeId}/state`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  releaseProductionOrderToMes: (productionOrderId: string) =>
    request<ProductionOrderListItem>(`/api/erp/production-orders/${productionOrderId}/release-to-mes`, {
      method: 'POST',
    }),
}

export const mesApi = {
  getWorkOrders: () => request<WorkOrderListItem[]>('/api/mes/work-orders'),
  dispatchWorkOrder: (workOrderId: string) =>
    request<WorkOrderListItem>(`/api/mes/work-orders/${workOrderId}/dispatch`, { method: 'POST' }),
  startWorkOrder: (workOrderId: string) =>
    request<WorkOrderListItem>(`/api/mes/work-orders/${workOrderId}/start`, { method: 'POST' }),
  reportWorkOrder: (
    workOrderId: string,
    payload: {
      goodQuantity: number
      scrapQuantity: number
      reworkQuantity: number
      laborHours: number
      machineHours: number
      scrapReasonCode: string | null
    },
  ) =>
    request<WorkOrderListItem>(`/api/mes/work-orders/${workOrderId}/report`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
}

export const qualityApi = {
  getInspections: () => request<QualityInspectionItem[]>('/api/quality/inspections'),
  completeInspection: (
    inspectionId: string,
    payload: {
      disposition: string
      defectQuantity: number
      notes: string | null
    },
  ) =>
    request<QualityInspectionItem>(`/api/quality/inspections/${inspectionId}/complete`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
}

export const aiApi = {
  getManagementSummary: () => request<AiManagementSummary>('/api/ai/management-summary'),
}

export const integrationApi = {
  getExternalSystems: () => request<ExternalSystemStatus[]>('/api/integration/external-systems'),
  getAccountingExportBatches: () => request<AccountingExportBatch[]>('/api/integration/accounting-export-batches'),
  createAccountingExportBatch: (payload: { targetSystemCode: string }) =>
    request<AccountingExportBatch>('/api/integration/accounting-export-batches', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  dispatchAccountingExportBatch: (exportBatchId: string) =>
    request<IntegrationJobItem>(`/api/integration/accounting-export-batches/${exportBatchId}/dispatch`, {
      method: 'POST',
    }),
  getIntegrationJobs: () => request<IntegrationJobItem[]>('/api/integration/jobs'),
  reconcileIntegrationJob: (integrationJobId: string, payload?: { notes: string | null }) =>
    request<IntegrationJobItem>(`/api/integration/jobs/${integrationJobId}/reconcile`, {
      method: 'POST',
      body: JSON.stringify({ notes: payload?.notes ?? null }),
    }),
  completeIntegrationJob: (
    integrationJobId: string,
    payload: {
      outcome: string
      errorMessage: string | null
    },
  ) =>
    request<IntegrationJobItem>(`/api/integration/jobs/${integrationJobId}/complete`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
}
