import type { ModuleDefinition } from '../../types/module-definition'

export const erpModule: ModuleDefinition = {
  key: 'erp',
  title: 'ERP',
  phase: 'Phase 1',
  summary: 'Procurement, inventory, master data, and finance-support records.',
  description:
    'Handles suppliers, materials, stock movement, procurement execution, and the cost-support records needed for finance and management reporting.',
  capabilities: [
    'Supplier and procurement management',
    'Warehouse transactions and stock visibility',
    'Material, product, and BOM master data',
    'Cost record collection and finance handoff',
  ],
  placeholderEntities: ['Supplier', 'Material', 'Product', 'BillOfMaterials', 'Warehouse', 'InventoryTransaction', 'PurchaseOrder', 'GoodsReceipt', 'CostRecord'],
  integrationTouchpoints: [
    'Inventory and voucher exports to Kingdee',
    'Material consumption inputs from MES',
    'Cost and stock data into reporting dashboards',
  ],
  deliveryFocus: [
    'Master data standardization',
    'Procure-to-stock reliability',
    'Cost-accounting readiness',
  ],
}
