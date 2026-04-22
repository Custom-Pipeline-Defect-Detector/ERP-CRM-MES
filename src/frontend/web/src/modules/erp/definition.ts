import type { ModuleDefinition } from '../../types/module-definition'

export const erpModule: ModuleDefinition = {
  key: 'erp',
  title: 'ERP',
  phase: 'Phase 1',
  summary: 'Procurement, inventory, shipping, master data, and finance-support records.',
  description:
    'Handles suppliers, materials, purchase orders, goods receipts, stock movement, shipment execution, and the bridge records needed for finance and management reporting.',
  capabilities: [
    'Supplier, purchase-order, and receipt management',
    'Warehouse transactions and stock visibility',
    'Sales shipment execution against released finished-goods stock',
    'Material, product, and BOM master data',
    'Cost record collection and accounting bridge handoff',
  ],
  placeholderEntities: ['Supplier', 'Material', 'Product', 'BillOfMaterials', 'Warehouse', 'InventoryTransaction', 'PurchaseOrder', 'GoodsReceipt', 'SalesShipment', 'CostRecord'],
  integrationTouchpoints: [
    'Inventory and voucher exports to Kingdee',
    'Material consumption inputs from MES',
    'Shipment and revenue/cost bridge outputs to finance',
    'Cost and stock data into reporting dashboards',
  ],
  deliveryFocus: [
    'Master data standardization',
    'Procure-to-stock reliability',
    'Ship-to-finance traceability',
    'Cost-accounting readiness',
  ],
}
