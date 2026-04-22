import type {
  AccountingBridgeItem,
  AccountingExportBatch,
  AiManagementSummary,
  AuditLogItem,
  CostLedgerItem,
  CustomerListItem,
  DashboardSnapshot,
  ExternalSystemStatus,
  GoodsReceiptListItem,
  IntegrationJobItem,
  InventoryBalanceItem,
  MaterialListItem,
  PurchaseOrderListItem,
  ProductListItem,
  ProductionOrderListItem,
  QualityInspectionItem,
  SalesOrderListItem,
  SalesShipmentListItem,
  Session,
  SupplierListItem,
  UserListItem,
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
  login: (userName: string) =>
    request<Session>('/api/platform/session/login', {
      method: 'POST',
      body: JSON.stringify({ userName }),
    }),
  getSession: () => request<Session>('/api/platform/session'),
  getUsers: () => request<UserListItem[]>('/api/platform/users'),
  getAuditLogs: (take = 20) => request<AuditLogItem[]>(`/api/platform/audit-logs?take=${take}`),
}

export const reportingApi = {
  getDashboard: () => request<DashboardSnapshot>('/api/reporting/dashboard'),
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
