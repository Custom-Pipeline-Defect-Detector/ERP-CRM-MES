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
