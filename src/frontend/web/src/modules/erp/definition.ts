import type { ModuleDefinition } from '../../types/module-definition'

export const erpModule: ModuleDefinition = {
  key: 'erp',
  title: 'ERP',
  phase: 'Phase 1',
  summary: 'Procurement, inventory, shipping, invoice control, vouchers, aging, and period-close support.',
  description:
    'Handles suppliers, materials, purchase orders, goods receipts, warehouse transfers, shipment execution, settlement support, invoice issuance, voucher control, aging analysis, accounting-period control, and the bridge records needed for finance and management reporting.',
  capabilities: [
    'Supplier, purchase-order, and receipt management',
    'Warehouse transactions, internal transfers, and stock visibility',
    'Sales shipment execution against released finished-goods stock',
    'Receivable and payable support entries with invoice, settlement, and aging actions',
    'Balanced accounting voucher creation, approval, posting, and period-close dependency checks',
    'Month-end accounting-period control with bridge, invoice, and voucher dependencies',
    'Material, product, and BOM master data',
    'Cost record collection, accounting voucher control, and accounting bridge handoff',
  ],
  placeholderEntities: ['Supplier', 'Material', 'Product', 'BillOfMaterials', 'Warehouse', 'InventoryTransaction', 'PurchaseOrder', 'GoodsReceipt', 'SalesShipment', 'CostRecord'],
  integrationTouchpoints: [
    'Inventory and voucher exports to Kingdee',
    'Material consumption inputs from MES',
    'Shipment and revenue/cost bridge outputs to finance',
    'Operational receivable, payable, invoice, and settlement handoff to finance',
    'Cost and stock data into reporting dashboards',
  ],
  deliveryFocus: [
    'Master data standardization',
    'Procure-to-stock reliability',
    'Ship-to-invoice-to-cash traceability',
    'Cost-accounting readiness',
  ],
}
