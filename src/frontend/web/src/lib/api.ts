import type {
  AuditLogItem,
  CustomerListItem,
  DashboardSnapshot,
  ProductListItem,
  ProductionOrderListItem,
  SalesOrderListItem,
  Session,
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
  getProductionOrders: () => request<ProductionOrderListItem[]>('/api/erp/production-orders'),
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
